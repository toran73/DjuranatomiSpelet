// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DjuranatomiSpelet.Core;
using DjuranatomiSpelet.Views;

namespace DjuranatomiSpelet;

/// <summary>The game screen, operated by the teacher (mouse or keyboard).</summary>
public partial class MainWindow : Window
{
    private const int StepDelayMs = 230;

    private readonly GameEngine _game;
    private readonly BoardView _board = new();
    private readonly Random _rng = new();
    private readonly List<Border> _tokens = new();
    private readonly int[] _shownPosition; // where each token is drawn (lags behind the engine while animating)
    private bool _busy;

    public MainWindow(Animal animal, IReadOnlyList<Question> questions, GameConfig config)
    {
        InitializeComponent();

        _game = new GameEngine(config.PlayerNames, animal, config.Difficulty, questions);
        _shownPosition = new int[_game.Players.Count];

        Title = $"{App.GameName} – {animal.Name} – {config.Difficulty?.DisplayName() ?? "Blandat"}";

        BoardHost.Child = _board;
        _board.SetAnimal(animal);
        BuildLegend();
        CreateTokens();
        DrawDie(1);
        UpdateUi();

        // Tunnel so the keys work even when a button has focus (and Space doesn't also "click" it).
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private double TokenSize => _board.Radius * 1.5;

    // ================================================================ actions

    private async Task RollAsync()
    {
        if (_busy || _game.Phase != TurnPhase.WaitingForRoll) return;
        _busy = true;
        FeedbackText.Text = "";
        UpdateUi();

        for (int i = 0; i < 10; i++)
        {
            DrawDie(_rng.Next(1, 7));
            await Task.Delay(55);
        }

        var move = _game.Roll();
        DrawDie(move.Roll);
        FeedbackText.Foreground = Brushes.Black;
        FeedbackText.Text = $"{move.Player.Name} slog {move.Roll}.";

        await AnimateTokenAsync(move.Player, move.To);

        _game.Arrive();
        if (_game.LandedOnEmpty)
            FeedbackText.Text = $"{move.Player.Name} slog {move.Roll} och landade på en tom ruta – ingen fråga.";
        _busy = false;
        UpdateUi();
    }

    private void ShowAnswer()
    {
        if (_busy || _game.Phase != TurnPhase.ShowingQuestion) return;
        _game.RevealAnswer();
        UpdateUi();
    }

    private async Task JudgeAsync(bool correct)
    {
        if (_busy || _game.Phase != TurnPhase.ShowingAnswer) return;

        var result = _game.Judge(correct);
        var player = result.Player;

        if (result.Won)
        {
            FeedbackText.Text = "";
            UpdateUi();
            WinnerText.Text = player.Name;
            WinnerText.Foreground = Palette.PlayerBrush(player.Number);
            WinnerOverlay.IsVisible = true;
            return;
        }

        if (correct)
        {
            FeedbackText.Foreground = Palette.Brush("#16A34A");
            FeedbackText.Text = $"✔ Rätt! {player.Name} står kvar på ruta {result.Space}.";
            UpdateUi();
            return;
        }

        FeedbackText.Foreground = Palette.Brush("#D62828");
        FeedbackText.Text = result.MovedBackTo == 0
            ? $"✘ Fel – {player.Name} går tillbaka till START."
            : $"✘ Fel – {player.Name} går tillbaka till ruta {result.MovedBackTo}.";

        // Keep the question and answer visible while the token walks back.
        _busy = true;
        SetButtons(false, false, false, false);
        await AnimateTokenAsync(player, result.MovedBackTo ?? player.Position);
        _busy = false;
        UpdateUi();
    }

    private void OtherQuestion()
    {
        if (_busy || (_game.Phase != TurnPhase.ShowingQuestion && _game.Phase != TurnPhase.ShowingAnswer)) return;
        _game.DrawAnotherQuestion();
        UpdateUi();
    }

    // ================================================================ board extras

    private void BuildLegend()
    {
        LegendPanel.Children.Clear();
        foreach (var part in _game.Animal.Parts)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2), Spacing = 6 };
            row.Children.Add(new Ellipse { Width = 18, Height = 18, Fill = Palette.PartBrush(part), Stroke = Palette.Outline, StrokeThickness = 1.5, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = part.Name, FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
            LegendPanel.Children.Add(row);
        }
    }

    private void CreateTokens()
    {
        var size = TokenSize;
        foreach (var player in _game.Players)
        {
            var token = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = Palette.PlayerBrush(player.Number),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(size * 0.1),
                RenderTransformOrigin = RelativePoint.Center,
                Child = new TextBlock
                {
                    Text = Initial(player.Name),
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.Black,
                    FontSize = size * 0.42,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _tokens.Add(token);
            _board.TokenLayer.Children.Add(token);
        }
        PositionTokens();
    }

    /// <summary>Swedish genitive: "Anna" -> "Annas", "Lars" -> "Lars".</summary>
    private static string Genitive(string name) =>
        name.EndsWith('s') || name.EndsWith('x') || name.EndsWith('z') ? name : name + "s";

    private static string Initial(string name)
    {
        // "Spelare 3" -> "3", "Anna" -> "A", "Lag Blå" -> "B"
        var last = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "?";
        return int.TryParse(last, out _) ? last : last[..1].ToUpperInvariant();
    }

    private void PositionTokens()
    {
        var current = _game.Phase == TurnPhase.GameOver ? _game.Winner : _game.CurrentPlayer;
        var size = TokenSize;
        var spread = _board.Radius * 0.75;
        var shadow = BoxShadows.Parse(string.Format(CultureInfo.InvariantCulture, "0 0 {0:0} 0 #E6000000", size * 0.45));

        foreach (var group in _game.Players.GroupBy(p => _shownPosition[p.Number]))
        {
            var center = _board.ToPixel(_game.Spaces[group.Key]);
            var members = group.ToList();
            for (int k = 0; k < members.Count; k++)
            {
                var player = members[k];
                double dx = 0, dy = 0;
                if (members.Count > 1)
                {
                    var angle = -Math.PI / 2 + 2 * Math.PI * k / members.Count;
                    dx = spread * Math.Cos(angle);
                    dy = spread * Math.Sin(angle);
                }

                var token = _tokens[player.Number];
                Canvas.SetLeft(token, center.X + dx - size / 2);
                Canvas.SetTop(token, center.Y + dy - size / 2);

                var isCurrent = player == current;
                token.ZIndex = isCurrent ? 100 : player.Number;
                token.RenderTransform = isCurrent ? new ScaleTransform(1.3, 1.3) : null;
                token.BoxShadow = isCurrent ? shadow : default;
            }
        }
    }

    private async Task AnimateTokenAsync(Player player, int target)
    {
        var i = player.Number;
        var step = Math.Sign(target - _shownPosition[i]);
        while (_shownPosition[i] != target)
        {
            _shownPosition[i] += step;
            PositionTokens();
            await Task.Delay(StepDelayMs);
        }
    }

    // ================================================================ die

    private void DrawDie(int value)
    {
        DieCanvas.Children.Clear();
        var face = new Border
        {
            Width = 100,
            Height = 100,
            CornerRadius = new CornerRadius(18),
            Background = Brushes.White,
            BorderBrush = Palette.Outline,
            BorderThickness = new Thickness(4)
        };
        Canvas.SetLeft(face, 5);
        Canvas.SetTop(face, 5);
        DieCanvas.Children.Add(face);

        double l = 30, m = 55, r = 80;
        var pips = value switch
        {
            1 => new[] { (m, m) },
            2 => new[] { (l, l), (r, r) },
            3 => new[] { (l, l), (m, m), (r, r) },
            4 => new[] { (l, l), (r, l), (l, r), (r, r) },
            5 => new[] { (l, l), (r, l), (m, m), (l, r), (r, r) },
            _ => new[] { (l, l), (r, l), (l, m), (r, m), (l, r), (r, r) }
        };
        foreach (var (x, y) in pips)
        {
            var pip = new Ellipse { Width = 18, Height = 18, Fill = Palette.Outline };
            Canvas.SetLeft(pip, x - 9);
            Canvas.SetTop(pip, y - 9);
            DieCanvas.Children.Add(pip);
        }
    }

    // ================================================================ UI state

    private void UpdateUi()
    {
        var phase = _game.Phase;
        var player = _game.CurrentPlayer;

        if (phase == TurnPhase.GameOver)
        {
            TurnText.Text = "Spelet är slut";
            TurnText.Foreground = Palette.Outline;
        }
        else
        {
            TurnText.Text = $"{Genitive(player.Name)} tur";
            TurnText.Foreground = Palette.PlayerBrush(player.Number);
        }

        RebuildPlayerChips();
        PositionTokens();

        var asking = phase is TurnPhase.ShowingQuestion or TurnPhase.ShowingAnswer;
        if (asking)
        {
            var space = _game.CurrentSpace;
            var part = space.Part!; // empty spaces never reach the asking phase
            var q = _game.CurrentQuestion;
            var isGoal = _game.TargetSpace == _game.GoalIndex;

            PartBadge.IsVisible = true;
            PartBadge.Background = Palette.PartBrush(part);
            PartBadgeText.Text = isGoal
                ? $"{part.Name} – MÅL! Svara rätt för att vinna"
                : $"{part.Name} · ruta {_game.TargetSpace}" + (q is null ? "" : $" · {q.Difficulty.DisplayName()}");
            QuestionCard.BorderBrush = Palette.PartBrush(part);

            QuestionText.Text = q?.Text ?? $"Det finns ingen fråga för “{part.Name}” – hitta på en egen!";
            AnswerText.Text = string.IsNullOrWhiteSpace(q?.Answer) ? "(inget svar sparat)" : q!.Answer;
            AnswerBox.IsVisible = phase == TurnPhase.ShowingAnswer;
        }
        else if (!_busy)
        {
            PartBadge.IsVisible = false;
            QuestionCard.BorderBrush = phase == TurnPhase.GameOver ? Palette.Outline : Palette.PlayerBrush(player.Number);
            QuestionText.Text = phase == TurnPhase.GameOver ? "" : $"{player.Name}, slå tärningen!";
            AnswerBox.IsVisible = false;
        }

        SetButtons(
            roll: !_busy && phase == TurnPhase.WaitingForRoll,
            showAnswer: !_busy && phase == TurnPhase.ShowingQuestion,
            judge: !_busy && phase == TurnPhase.ShowingAnswer,
            other: !_busy && asking);
    }

    private void SetButtons(bool roll, bool showAnswer, bool judge, bool other)
    {
        RollButton.IsEnabled = roll;
        ShowAnswerButton.IsEnabled = showAnswer;
        CorrectButton.IsEnabled = judge;
        WrongButton.IsEnabled = judge;
        NewQuestionButton.IsEnabled = other;
    }

    private void RebuildPlayerChips()
    {
        PlayersPanel.Children.Clear();
        foreach (var p in _game.Players)
        {
            var isCurrent = p == _game.CurrentPlayer && _game.Phase != TurnPhase.GameOver;
            var pos = p.Position == 0 ? "start" : $"ruta {p.Position}";
            PlayersPanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 4, 12, 4),
                Margin = new Thickness(0, 0, 6, 6),
                BorderThickness = new Thickness(3),
                BorderBrush = isCurrent ? Palette.PlayerBrush(p.Number) : Brushes.Transparent,
                Background = Palette.Brush("#F3EEE8"),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new Ellipse { Width = 16, Height = 16, Fill = Palette.PlayerBrush(p.Number), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = $"{p.Name} ({pos})", FontSize = 16, FontWeight = isCurrent ? FontWeight.Bold : FontWeight.Normal }
                    }
                }
            });
        }
    }

    // ================================================================ events

    private async void Roll_Click(object? sender, RoutedEventArgs e) => await RollAsync();
    private void ShowAnswer_Click(object? sender, RoutedEventArgs e) => ShowAnswer();
    private async void Correct_Click(object? sender, RoutedEventArgs e) => await JudgeAsync(true);
    private async void Wrong_Click(object? sender, RoutedEventArgs e) => await JudgeAsync(false);
    private void NewQuestion_Click(object? sender, RoutedEventArgs e) => OtherQuestion();

    /// <summary>Keys: Space/T = slå, S = visa svar, R = rätt, F = fel, A = annan fråga, F11/Esc = helskärm.</summary>
    private async void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (WinnerOverlay.IsVisible) return;

        switch (e.Key)
        {
            case Key.Space:
            case Key.T:
                e.Handled = true;
                await RollAsync();
                break;
            case Key.S:
                e.Handled = true;
                ShowAnswer();
                break;
            case Key.R:
                e.Handled = true;
                await JudgeAsync(true);
                break;
            case Key.F:
                e.Handled = true;
                await JudgeAsync(false);
                break;
            case Key.A:
                e.Handled = true;
                OtherQuestion();
                break;
            case Key.F11:
                e.Handled = true;
                WindowState = WindowState == WindowState.FullScreen ? WindowState.Maximized : WindowState.FullScreen;
                break;
            case Key.Escape when WindowState == WindowState.FullScreen:
                e.Handled = true;
                WindowState = WindowState.Maximized;
                break;
        }
    }

    private void NewGame_Click(object? sender, RoutedEventArgs e)
    {
        new SetupWindow().Show();
        Close();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
