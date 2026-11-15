// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using DjuranatomiSpelet.Core;
using DjuranatomiSpelet.Views;

namespace DjuranatomiSpelet;

public partial class SetupWindow : Window
{
    private const int MaxPlayers = 6;
    private readonly List<TextBox> _nameBoxes = new();

    public SetupWindow()
    {
        InitializeComponent();
        WindowFit.ToWorkArea(this);

        for (int i = 1; i <= MaxPlayers; i++)
            PlayerCountBox.Items.Add(i);
        PlayerCountBox.SelectionChanged += (_, _) => RebuildNameBoxes();
        PlayerCountBox.SelectedIndex = 1; // 2 players

        AnimalBox.SelectionChanged += (_, _) => UpdateButtons();
        Opened += (_, _) => ReloadAnimals();
    }

    private AnimalInfo? SelectedAnimal => AnimalBox.SelectedItem as AnimalInfo;

    private void ReloadAnimals(int? selectId = null)
    {
        try
        {
            var keep = selectId ?? SelectedAnimal?.Id;
            var animals = App.Repository.ListAnimals();
            AnimalBox.ItemsSource = animals;
            AnimalBox.SelectedItem = animals.FirstOrDefault(a => a.Id == keep) ?? animals.FirstOrDefault();
            DbInfo.Text = $"Databas: {App.Repository.DbPath}";
        }
        catch (Exception ex)
        {
            DbInfo.Text = "Kunde inte öppna databasen: " + ex.Message;
            AnimalBox.ItemsSource = null;
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var has = SelectedAnimal is not null;
        EditBoardButton.IsEnabled = has;
        EditQuestionsButton.IsEnabled = has;
        DeleteAnimalButton.IsEnabled = has;
        StartButton.IsEnabled = has;
    }

    private int PlayerCount => PlayerCountBox.SelectedItem is int n ? n : 2;

    private void RebuildNameBoxes()
    {
        var count = PlayerCount;

        // Keep names already typed when the count changes.
        while (_nameBoxes.Count < count)
            _nameBoxes.Add(new TextBox { Text = $"Spelare {_nameBoxes.Count + 1}", FontSize = 20, Width = 300 });

        NamesPanel.Children.Clear();
        for (int i = 0; i < count; i++)
        {
            var box = _nameBoxes[i];
            if (box.Parent is Panel oldParent)
                oldParent.Children.Remove(box);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3), Spacing = 10 };
            row.Children.Add(new Ellipse { Width = 24, Height = 24, Fill = Palette.PlayerBrush(i), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(box);
            NamesPanel.Children.Add(row);
        }
    }

    private Difficulty? SelectedDifficulty =>
        EasyRadio.IsChecked == true ? Difficulty.Easy :
        MediumRadio.IsChecked == true ? Difficulty.Medium :
        HardRadio.IsChecked == true ? Difficulty.Hard :
        null; // Blandat

    private async void Start_Click(object? sender, RoutedEventArgs e) => await Dialogs.Guard(this, async () =>
    {
        if (SelectedAnimal is not { } info) return;

        var animal = App.Repository.LoadAnimal(info.Id);
        var problem = animal.Validate();
        if (problem is not null)
        {
            await Dialogs.Info(this, $"“{animal.Name}” kan inte spelas än:\n\n{problem}\n\nÖppna “Redigera bild & rutor…” och gör klart djuret.");
            return;
        }

        var questions = App.Repository.GetQuestions(info.Id);
        if (questions.Count == 0 &&
            !await Dialogs.Confirm(this, $"“{animal.Name}” har inga frågor än. Vill du starta ändå? (Då hittar du på frågorna själv.)"))
            return;

        var names = _nameBoxes.Take(PlayerCount)
            .Select((tb, i) => string.IsNullOrWhiteSpace(tb.Text) ? $"Spelare {i + 1}" : tb.Text.Trim())
            .ToList();

        new MainWindow(animal, questions, new GameConfig(info.Id, names, SelectedDifficulty)).Show();
        Close();
    });

    private async void NewAnimal_Click(object? sender, RoutedEventArgs e) => await Dialogs.Guard(this, async () =>
    {
        var editor = new BoardEditorWindow(App.Repository, null);
        await editor.ShowDialog(this);
        ReloadAnimals(editor.SavedAnimalId);
    });

    private async void EditBoard_Click(object? sender, RoutedEventArgs e) => await Dialogs.Guard(this, async () =>
    {
        if (SelectedAnimal is not { } info) return;
        await new BoardEditorWindow(App.Repository, info.Id).ShowDialog(this);
        ReloadAnimals(info.Id);
    });

    private async void EditQuestions_Click(object? sender, RoutedEventArgs e) => await Dialogs.Guard(this, async () =>
    {
        if (SelectedAnimal is not { } info) return;
        await new QuestionEditorWindow(App.Repository, info.Id).ShowDialog(this);
        ReloadAnimals(info.Id);
    });

    private async void DeleteAnimal_Click(object? sender, RoutedEventArgs e) => await Dialogs.Guard(this, async () =>
    {
        if (SelectedAnimal is not { } info) return;
        if (!await Dialogs.Confirm(this, $"Ta bort “{info.Name}” med alla rutor och {info.QuestionCount} frågor?\n\nDet går inte att ångra.",
                "Ta bort", "Avbryt"))
            return;
        App.Repository.DeleteAnimal(info.Id);
        ReloadAnimals();
    });
}
