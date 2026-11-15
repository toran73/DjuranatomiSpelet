// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DjuranatomiSpelet.Core;
using DjuranatomiSpelet.Data;
using DjuranatomiSpelet.Views;

namespace DjuranatomiSpelet;

/// <summary>Edit the questions of one animal, grouped by its body parts.</summary>
public partial class QuestionEditorWindow : Window
{
    public sealed record FilterOption(BodyPart? Part, string Name)
    {
        public override string ToString() => Name;
    }

    private readonly SqliteGameRepository _repository;
    private readonly Animal _animal;
    private readonly ObservableCollection<QuestionRow> _rows = new();
    private readonly DataGridCollectionView _view;
    private readonly List<int> _deletedIds = new();
    private bool _dirty;
    private bool _loading;
    private bool _closeConfirmed;

    public QuestionEditorWindow(SqliteGameRepository repository, int animalId)
    {
        InitializeComponent();
        WindowFit.ToWorkArea(this);
        _repository = repository;
        _animal = repository.LoadAnimal(animalId);

        HeaderText.Text = $"Frågor – {_animal.Name}";

        _rows.CollectionChanged += Rows_CollectionChanged;
        _view = new DataGridCollectionView(_rows);
        QuestionGrid.ItemsSource = _view;

        var filters = new List<FilterOption> { new(null, "Alla kroppsdelar") };
        filters.AddRange(_animal.Parts.Select(p => new FilterOption(p, p.Name)));
        FilterBox.ItemsSource = filters;
        FilterBox.SelectionChanged += (_, _) => ApplyFilter();
        FilterBox.SelectedIndex = 0;

        Closing += Window_Closing;
        Load();

        if (_animal.Parts.Count == 0)
            StatusText.Text = "Djuret har inga kroppsdelar än – skapa dem först under “Redigera bild & rutor…”.";
    }

    private BodyPart? FilterPart => (FilterBox.SelectedItem as FilterOption)?.Part;

    private QuestionRow NewRow(Question q)
    {
        var row = new QuestionRow(q, _animal.Parts);
        row.PropertyChanged += Row_PropertyChanged;
        return row;
    }

    private void Load()
    {
        _loading = true;
        _rows.Clear();
        foreach (var q in _repository.GetQuestions(_animal.Id))
            _rows.Add(NewRow(q));
        _deletedIds.Clear();
        _dirty = false;
        _loading = false;
        UpdateStatus();
    }

    private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_loading) return;
        if (e.OldItems is not null)
            foreach (QuestionRow row in e.OldItems)
            {
                row.PropertyChanged -= Row_PropertyChanged;
                if (row.Model.Id > 0) _deletedIds.Add(row.Model.Id);
            }
        _dirty = true;
        UpdateStatus();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading) return;
        _dirty = true;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var parts = _animal.Parts.Select(p =>
        {
            var qs = _rows.Where(r => r.Model.BodyPartId == p.Id).Select(r => r.Model).ToList();
            return $"{p.Name}: {qs.Count(q => q.Difficulty == Difficulty.Easy)}/{qs.Count(q => q.Difficulty == Difficulty.Medium)}/{qs.Count(q => q.Difficulty == Difficulty.Hard)}";
        });
        StatusText.Text = $"{_rows.Count} frågor (lätt/medel/svår) – " + string.Join(" · ", parts) + (_dirty ? "   • Ej sparat" : "");
    }

    private void ApplyFilter()
    {
        var part = FilterPart;
        if (part is null)
            _view.Filter = null;
        else
            _view.Filter = o => o is QuestionRow r && r.Model.BodyPartId == part.Id;
        _view.Refresh();
    }

    private async void Add_Click(object? sender, RoutedEventArgs e)
    {
        var part = FilterPart ?? _animal.Parts.FirstOrDefault();
        if (part is null)
        {
            await Dialogs.Info(this, "Djuret har inga kroppsdelar än. Skapa dem först under “Redigera bild & rutor…”.");
            return;
        }

        QuestionGrid.CommitEdit();
        var row = NewRow(new Question { BodyPartId = part.Id, Difficulty = Difficulty.Easy });
        _rows.Add(row);

        // Let the grid create the new row before starting to edit the question cell.
        Dispatcher.UIThread.Post(() =>
        {
            QuestionGrid.SelectedItem = row;
            QuestionGrid.ScrollIntoView(row, QuestionGrid.Columns[2]);
            QuestionGrid.CurrentColumn = QuestionGrid.Columns[2];
            QuestionGrid.Focus();
            QuestionGrid.BeginEdit();
        }, DispatcherPriority.Background);
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        var selected = QuestionGrid.SelectedItems.OfType<QuestionRow>().ToList();
        if (selected.Count == 0) return;
        QuestionGrid.CancelEdit();
        foreach (var row in selected)
            _rows.Remove(row);
    }

    private async void Import_Click(object? sender, RoutedEventArgs e) => await Dialogs.Guard(this, async () =>
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importera frågor",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV-fil (semikolonseparerad)")
                {
                    Patterns = new[] { "*.csv", "*.txt" },
                    AppleUniformTypeIdentifiers = new[] { "public.comma-separated-values-text", "public.plain-text" },
                    MimeTypes = new[] { "text/csv", "text/plain" }
                },
                FilePickerFileTypes.All
            }
        });
        if (files.Count == 0) return;

        byte[] bytes;
        await using (var stream = await files[0].OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await stream.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var (imported, errors) = QuestionCsv.Read(DecodeText(bytes), _animal.Parts);
        foreach (var q in imported)
            _rows.Add(NewRow(q));

        var msg = $"{imported.Count} frågor importerades (glöm inte att spara).";
        if (errors.Count > 0)
            msg += $"\n\n{errors.Count} rader hoppades över:\n" + string.Join("\n", errors.Take(15)) + (errors.Count > 15 ? "\n…" : "");
        msg += $"\n\nKroppsdelar på det här djuret: {string.Join(", ", _animal.Parts.Select(p => p.Name))}";
        await Dialogs.Info(this, msg);
    });

    /// <summary>UTF-8 (with or without BOM); falls back to Latin-1 for files saved by older Excel versions.</summary>
    private static string DecodeText(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private async void Export_Click(object? sender, RoutedEventArgs e) => await Dialogs.Guard(this, async () =>
    {
        QuestionGrid.CommitEdit();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportera frågor",
            SuggestedFileName = $"Frågor - {_animal.Name}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV-fil (semikolonseparerad)") { Patterns = new[] { "*.csv" } }
            }
        });
        if (file is null) return;

        var csv = QuestionCsv.Write(_rows.Select(r => r.Model), _animal.Parts);
        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek) stream.SetLength(0);
        // UTF-8 with BOM so Excel shows å, ä, ö correctly.
        await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
        await writer.WriteAsync(csv);
        StatusText.Text = $"Exporterade {_rows.Count} frågor till {file.Name}";
    });

    private async Task<bool> SaveAsync()
    {
        QuestionGrid.CommitEdit();
        foreach (var row in _rows.Where(r => string.IsNullOrWhiteSpace(r.Model.Text)).ToList())
            _rows.Remove(row);

        try
        {
            _repository.SaveQuestions(_rows.Select(r => r.Model), _deletedIds);
        }
        catch (Exception ex)
        {
            await Dialogs.Info(this, "Kunde inte spara:\n\n" + ex.Message);
            return false;
        }
        Load();
        StatusText.Text = "Sparat! " + StatusText.Text;
        return true;
    }

    private async void Save_Click(object? sender, RoutedEventArgs e) => await SaveAsync();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    // Avalonia's Closing event can't wait for a dialog: cancel, ask, then close again.
    private async void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        QuestionGrid.CommitEdit();
        if (!_dirty || _closeConfirmed) return;
        e.Cancel = true;

        var answer = await Dialogs.AskSave(this);
        if (answer == DialogResult.Cancel) return;
        if (answer == DialogResult.Yes && !await SaveAsync()) return;

        _closeConfirmed = true;
        Close();
    }
}
