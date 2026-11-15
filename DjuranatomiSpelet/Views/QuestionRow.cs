// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using System.ComponentModel;
using System.Runtime.CompilerServices;
using DjuranatomiSpelet.Core;

namespace DjuranatomiSpelet.Views;

public sealed record DifficultyOption(Difficulty Value, string Name)
{
    public static IReadOnlyList<DifficultyOption> All { get; } =
        Enum.GetValues<Difficulty>().Select(d => new DifficultyOption(d, d.DisplayName())).ToList();

    public override string ToString() => Name;
}

/// <summary>One row in the question grid. Wraps a <see cref="Question"/> and exposes what the combo boxes need.</summary>
public sealed class QuestionRow : INotifyPropertyChanged
{
    public QuestionRow(Question model, IReadOnlyList<BodyPart> parts)
    {
        Model = model;
        Parts = parts;
    }

    public Question Model { get; }

    /// <summary>Choices for the body part combo box.</summary>
    public IReadOnlyList<BodyPart> Parts { get; }

    /// <summary>Choices for the difficulty combo box.</summary>
    public IReadOnlyList<DifficultyOption> Difficulties => DifficultyOption.All;

    public BodyPart? Part
    {
        get => Parts.FirstOrDefault(p => p.Id == Model.BodyPartId);
        set
        {
            if (value is null || value.Id == Model.BodyPartId) return;
            Model.BodyPartId = value.Id;
            OnChanged();
            OnChanged(nameof(PartName));
        }
    }

    public string PartName => Part?.Name ?? "";

    public DifficultyOption? Difficulty
    {
        get => DifficultyOption.All.FirstOrDefault(d => d.Value == Model.Difficulty);
        set
        {
            if (value is null || value.Value == Model.Difficulty) return;
            Model.Difficulty = value.Value;
            OnChanged();
            OnChanged(nameof(DifficultyName));
        }
    }

    public string DifficultyName => Model.Difficulty.DisplayName();

    public string Text
    {
        get => Model.Text;
        set
        {
            if (value == Model.Text) return;
            Model.Text = value ?? "";
            OnChanged();
        }
    }

    public string Answer
    {
        get => Model.Answer;
        set
        {
            if (value == Model.Answer) return;
            Model.Answer = value ?? "";
            OnChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
