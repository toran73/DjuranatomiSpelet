// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DjuranatomiSpelet.Core;
using DjuranatomiSpelet.Data;
using DjuranatomiSpelet.Views;

namespace DjuranatomiSpelet;

/// <summary>Create or edit an animal: image, body parts and the path of spaces.</summary>
public partial class BoardEditorWindow : Window
{
    private readonly SqliteGameRepository _repository;
    private readonly Animal _animal;
    private readonly BoardView _board = new();

    private int _selected = -1;
    private int _dragIndex = -1;
    private bool _dirty;
    private bool _updating;
    private bool _closeConfirmed;

    /// <param name="animalId">null = create a new animal.</param>
    public BoardEditorWindow(SqliteGameRepository repository, int? animalId)
    {
        InitializeComponent();
        WindowFit.ToWorkArea(this);
        _repository = repository;
        _animal = animalId is int id ? repository.LoadAnimal(id) : new Animal { Name = "Nytt djur" };

        BoardHost.Child = _board;
        _board.SpaceLayer.PointerPressed += Board_PointerPressed;
        _board.SpaceLayer.PointerMoved += Board_PointerMoved;
        _board.SpaceLayer.PointerReleased += Board_PointerReleased;
        _board.SetAnimal(_animal);

        _updating = true;
        NameBox.Text = _animal.Name;
        SizeSlider.Value = _animal.SpaceSize;
        _updating = false;

        NameBox.TextChanged += NameBox_TextChanged;
        PartNameBox.TextChanged += PartNameBox_TextChanged;
        PartsList.SelectionChanged += (_, _) => PartsListSelectionChanged();
        SizeSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty) OnSpaceSizeChanged();
        };
        KeyDown += Window_KeyDown;
        Closing += Window_Closing;

        RebuildPartsList(_animal.Parts.FirstOrDefault());
        Refresh();
        _dirty = false;
    }

    /// <summary>Id of the animal after saving (null if never saved).</summary>
    public int? SavedAnimalId { get; private set; }

    private BodyPart? SelectedPart => (PartsList.SelectedItem as ListBoxItem)?.Tag as BodyPart;

    // ================================================================ board mouse handling

    private void Board_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_board);
        var pos = point.Position;
        var hit = _board.HitTestSpace(pos);

        if (point.Properties.IsRightButtonPressed)
        {
            // Right-click on a space removes it; right-click on the image adds an empty space.
            if (hit >= 0)
                DeleteSpace(hit);
            else if (_animal.Image is null)
                StatusText.Text = "Välj en bild först.";
            else
                AddSpaceAt(pos, null);
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed) return;

        if (hit >= 0)
        {
            Select(hit);
            _dragIndex = hit;
            e.Pointer.Capture(_board.SpaceLayer);
            e.Handled = true;
            return;
        }

        if (_animal.Image is null)
        {
            StatusText.Text = "Välj en bild först.";
            return;
        }
        var empty = EmptyModeCheck.IsChecked == true;
        var part = SelectedPart;
        if (!empty && part is null)
        {
            StatusText.Text = "Skapa och markera en kroppsdel först (till höger).";
            return;
        }

        AddSpaceAt(pos, empty ? null : part);
        e.Handled = true;
    }

    /// <summary>Adds a space after the selected one (or last). part = null gives an empty space without a question.</summary>
    private void AddSpaceAt(Point pos, BodyPart? part)
    {
        var rel = _board.ToRelative(pos);
        var space = new BoardSpace { X = rel.X, Y = rel.Y, Part = part };
        var index = _selected >= 0 ? _selected + 1 : _animal.Spaces.Count;
        _animal.Spaces.Insert(index, space);
        _dirty = true;
        Select(index);
    }

    private void Board_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragIndex < 0) return;
        var point = e.GetCurrentPoint(_board);
        if (!point.Properties.IsLeftButtonPressed) return;

        var rel = _board.ToRelative(point.Position);
        var space = _animal.Spaces[_dragIndex];
        space.X = rel.X;
        space.Y = rel.Y;
        _dirty = true;
        _board.Redraw(_selected);
    }

    private void Board_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragIndex < 0) return;
        _dragIndex = -1;
        e.Pointer.Capture(null);
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (FocusManager?.GetFocusedElement() is TextBox) return;

        if ((e.Key == Key.Delete || e.Key == Key.Back) && _selected >= 0)
        {
            DeleteSpace(_selected);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Select(-1);
            e.Handled = true;
        }
    }

    // ================================================================ spaces

    private void Select(int index)
    {
        _selected = index;
        if (index >= 0)
        {
            // Show the space's body part, so new spaces after it continue with the same part.
            var part = _animal.Spaces[index].Part;
            var item = part is null ? null : PartsList.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag == part);
            if (item is not null)
            {
                // Selecting a normal space switches back to normal (question) mode.
                _updating = true;
                EmptyModeCheck.IsChecked = false;
                _updating = false;
                PartsList.SelectedItem = item;
            }
        }
        Refresh();
    }

    private void DeleteSpace(int index)
    {
        if (index < 0 || index >= _animal.Spaces.Count) return;
        _animal.Spaces.RemoveAt(index);
        _dirty = true;
        _selected = _animal.Spaces.Count == 0 ? -1 : Math.Min(index, _animal.Spaces.Count) - 1;
        Refresh();
    }

    private void AssignPart_Click(object? sender, RoutedEventArgs e)
    {
        if (_selected < 0 || SelectedPart is not { } part) return;
        _animal.Spaces[_selected].Part = part;
        _dirty = true;
        Refresh();
    }

    private void MakeEmpty_Click(object? sender, RoutedEventArgs e)
    {
        if (_selected < 0) return;
        _animal.Spaces[_selected].Part = null;
        _dirty = true;
        Refresh();
    }

    private void EmptyModeCheck_Changed(object? sender, RoutedEventArgs e)
    {
        if (_updating) return;
        UpdateButtons();
    }

    private void DeleteSpace_Click(object? sender, RoutedEventArgs e) => DeleteSpace(_selected);

    private async void ClearSpaces_Click(object? sender, RoutedEventArgs e)
    {
        if (_animal.Spaces.Count == 0) return;
        if (!await Dialogs.Confirm(this, $"Ta bort alla {_animal.Spaces.Count} rutor?")) return;
        _animal.Spaces.Clear();
        _selected = -1;
        _dirty = true;
        Refresh();
    }

    // ================================================================ body parts

    private void RebuildPartsList(BodyPart? select)
    {
        _updating = true;
        PartsList.Items.Clear();
        foreach (var part in _animal.Parts)
            PartsList.Items.Add(new ListBoxItem { Tag = part, Content = PartItemContent(part), Padding = new Thickness(6, 4) });
        PartsList.SelectedItem = PartsList.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag == select);
        _updating = false;
        PartsListSelectionChanged();
    }

    private Control PartItemContent(BodyPart part)
    {
        var count = _animal.Spaces.Count(s => s.Part == part);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new Ellipse { Width = 20, Height = 20, Fill = Palette.PartBrush(part), Stroke = Palette.Outline, StrokeThickness = 1.5, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = string.IsNullOrWhiteSpace(part.Name) ? "(namnlös)" : part.Name, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $"{count} rutor", Foreground = Brushes.Gray, FontSize = 13, VerticalAlignment = VerticalAlignment.Center }
            }
        };
    }

    private void RefreshPartItems()
    {
        foreach (var item in PartsList.Items.OfType<ListBoxItem>())
            if (item.Tag is BodyPart part)
                item.Content = PartItemContent(part);
    }

    private void PartsListSelectionChanged()
    {
        if (_updating) return;
        _updating = true;
        PartNameBox.Text = SelectedPart?.Name ?? "";
        PartNameBox.IsEnabled = SelectedPart is not null;
        if (SelectedPart is not null) EmptyModeCheck.IsChecked = false; // picking a body part = back to question spaces
        _updating = false;
        UpdateButtons();
    }

    private void PartNameBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating || SelectedPart is not { } part) return;
        part.Name = PartNameBox.Text ?? "";
        _dirty = true;
        RefreshPartItems();
    }

    private void AddPart_Click(object? sender, RoutedEventArgs e)
    {
        var part = new BodyPart
        {
            Name = $"Kroppsdel {_animal.Parts.Count + 1}",
            Color = Palette.NextPartColor(_animal.Parts)
        };
        _animal.Parts.Add(part);
        _dirty = true;
        RebuildPartsList(part);
        PartNameBox.Focus();
        PartNameBox.SelectAll();
    }

    private void CycleColor_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedPart is not { } part) return;
        part.Color = Palette.CycleColor(part.Color);
        _dirty = true;
        RefreshPartItems();
        _board.Redraw(_selected);
    }

    private async void DeletePart_Click(object? sender, RoutedEventArgs e)
    {
        if (SelectedPart is not { } part) return;

        var spaces = _animal.Spaces.Count(s => s.Part == part);
        var questions = part.Id != 0 && _animal.Id != 0
            ? _repository.QuestionCountsByPart(_animal.Id).GetValueOrDefault(part.Id)
            : 0;

        if (spaces > 0 || questions > 0)
        {
            var msg = $"Ta bort kroppsdelen “{part.Name}”?\n\n" +
                      (spaces > 0 ? $"• {spaces} rutor som hör till den tas bort.\n" : "") +
                      (questions > 0 ? $"• {questions} frågor tas bort när du sparar.\n" : "");
            if (!await Dialogs.Confirm(this, msg, "Ta bort", "Avbryt")) return;
        }

        _animal.Spaces.RemoveAll(s => s.Part == part);
        _animal.Parts.Remove(part);
        _selected = -1;
        _dirty = true;
        RebuildPartsList(_animal.Parts.FirstOrDefault());
        Refresh();
    }

    // ================================================================ animal settings

    private void NameBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        _animal.Name = NameBox.Text ?? "";
        _dirty = true;
    }

    private void OnSpaceSizeChanged()
    {
        if (_updating) return;
        _animal.SpaceSize = SizeSlider.Value;
        _dirty = true;
        _board.Redraw(_selected);
    }

    private async void ChooseImage_Click(object? sender, RoutedEventArgs e) => await Dialogs.Guard(this, async () =>
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Välj en bild på djuret",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Bilder")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" },
                    AppleUniformTypeIdentifiers = new[] { "public.image" },
                    MimeTypes = new[] { "image/*" }
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

        if (BoardView.LoadBitmap(bytes) is null)
        {
            await Dialogs.Info(this, "Bilden kunde inte läsas. Prova en PNG- eller JPG-fil.");
            return;
        }

        _animal.Image = bytes;
        if (_animal.Name is "" or "Nytt djur")
        {
            _animal.Name = System.IO.Path.GetFileNameWithoutExtension(files[0].Name);
            _updating = true;
            NameBox.Text = _animal.Name;
            _updating = false;
        }
        _dirty = true;
        _board.RefreshImage();
        Refresh();

        if (bytes.Length > 15 * 1024 * 1024)
            StatusText.Text = "Tips: bilden är stor – en mindre bild (t.ex. 2000 px bred) gör databasen mindre.";
    });

    // ================================================================ refresh / save

    private void Refresh()
    {
        if (_selected >= _animal.Spaces.Count) _selected = _animal.Spaces.Count - 1;
        _board.Redraw(_selected);
        NoImageText.IsVisible = _animal.Image is null;
        RefreshPartItems();

        if (_selected >= 0)
        {
            var label = _selected == 0 ? "START" : _selected == _animal.Spaces.Count - 1 ? "MÅL" : $"Ruta {_selected}";
            SelectedSpaceText.Text = $"{label} – {_animal.Spaces[_selected].Part?.Name ?? "tom ruta (ingen fråga)"}";
        }
        else
        {
            SelectedSpaceText.Text = "Ingen ruta markerad (nya rutor läggs sist).";
        }

        var problem = _animal.Validate();
        StatusText.Text = problem is null
            ? $"{_animal.Spaces.Count} rutor, {_animal.Parts.Count} kroppsdelar – klart att spela."
            : $"{_animal.Spaces.Count} rutor. Att göra: {problem}";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        AssignPartButton.IsEnabled = _selected >= 0 && SelectedPart is not null;
        MakeEmptyButton.IsEnabled = _selected >= 0;
        DeleteSpaceButton.IsEnabled = _selected >= 0;
    }

    private async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_animal.Name))
        {
            await Dialogs.Info(this, "Ge djuret ett namn innan du sparar.");
            NameBox.Focus();
            return false;
        }
        for (int i = 0; i < _animal.Parts.Count; i++)
            if (string.IsNullOrWhiteSpace(_animal.Parts[i].Name))
                _animal.Parts[i].Name = $"Kroppsdel {i + 1}";

        var dupes = _animal.Parts.GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0)
        {
            await Dialogs.Info(this, $"Två kroppsdelar har samma namn: {string.Join(", ", dupes)}. Byt namn på en av dem.");
            return false;
        }

        try
        {
            SavedAnimalId = _repository.SaveAnimal(_animal);
        }
        catch (Exception ex)
        {
            await Dialogs.Info(this, "Kunde inte spara:\n\n" + ex.Message);
            return false;
        }
        _dirty = false;

        var problem = _animal.Validate();
        Refresh();
        StatusText.Text = problem is null ? "Sparat! Djuret är klart att spela." : $"Sparat, men djuret kan inte spelas än: {problem}";
        return true;
    }

    private async void Save_Click(object? sender, RoutedEventArgs e) => await SaveAsync();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    // Avalonia's Closing event can't wait for a dialog: cancel, ask, then close again.
    private async void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (!_dirty || _closeConfirmed) return;
        e.Cancel = true;

        var answer = await Dialogs.AskSave(this);
        if (answer == DialogResult.Cancel) return;
        if (answer == DialogResult.Yes && !await SaveAsync()) return;

        _closeConfirmed = true;
        Close();
    }
}
