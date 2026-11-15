// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

namespace DjuranatomiSpelet.Core;

public enum Difficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public static class DifficultyInfo
{
    public static string DisplayName(this Difficulty d) => d switch
    {
        Difficulty.Easy => "Lätt",
        Difficulty.Medium => "Medel",
        Difficulty.Hard => "Svår",
        _ => d.ToString()
    };

    /// <summary>Accepts Swedish names (Lätt/Medel/Svår), English names (Easy/Medium/Hard) or 1/2/3.</summary>
    public static bool TryParse(string? value, out Difficulty difficulty)
    {
        var v = (value ?? "").Trim();
        foreach (var d in Enum.GetValues<Difficulty>())
        {
            if (v.Equals(d.DisplayName(), StringComparison.OrdinalIgnoreCase) ||
                v.Equals(d.ToString(), StringComparison.OrdinalIgnoreCase) ||
                v == ((int)d).ToString())
            {
                difficulty = d;
                return true;
            }
        }
        difficulty = Difficulty.Easy;
        return false;
    }
}

/// <summary>A group of spaces on an animal, e.g. "Huvud". Questions belong to a body part.</summary>
public class BodyPart
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Colour as #RRGGBB.</summary>
    public string Color { get; set; } = "#888888";

    public override string ToString() => Name;
}

/// <summary>A space on the board. X and Y are relative to the image (0..1), so any image size works.</summary>
public class BoardSpace
{
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>The body part (and thus the questions) of this space. null = empty space without a question.</summary>
    public BodyPart? Part { get; set; }

    public bool IsEmpty => Part is null;
}

/// <summary>An animal = an image + body parts + an ordered path of spaces.</summary>
public class Animal
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>The image file's bytes (PNG/JPG). Stored in the database so the database file is self-contained.</summary>
    public byte[]? Image { get; set; }

    /// <summary>Space radius relative to the image's shorter side.</summary>
    public double SpaceSize { get; set; } = 0.033;

    public List<BodyPart> Parts { get; set; } = new();

    /// <summary>The path in walking order. The first space is START (no question), the last is the GOAL.</summary>
    public List<BoardSpace> Spaces { get; set; } = new();

    public const int MinSpaces = 3;

    /// <summary>Returns a message describing what is missing, or null when the animal can be played.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return "Djuret behöver ett namn.";
        if (Image is null || Image.Length == 0) return "Välj en bild.";
        if (Parts.Count == 0) return "Lägg till minst en kroppsdel.";
        if (Spaces.Count < MinSpaces) return $"Placera ut minst {MinSpaces} rutor (start, minst en ruta och mål).";
        if (Spaces.Any(s => s.Part is not null && !Parts.Contains(s.Part))) return "Alla rutor måste tillhöra en kroppsdel eller vara tomma.";
        if (Spaces[^1].IsEmpty) return "Sista rutan (MÅL) måste tillhöra en kroppsdel, eftersom man måste svara rätt för att vinna.";
        return null;
    }
}

public class Question
{
    public int Id { get; set; }
    public int BodyPartId { get; set; }
    public Difficulty Difficulty { get; set; } = Difficulty.Easy;
    public string Text { get; set; } = "";
    public string Answer { get; set; } = "";
}

public sealed record AnimalInfo(int Id, string Name, int SpaceCount, int QuestionCount)
{
    public override string ToString() => $"{Name}  ({SpaceCount} rutor, {QuestionCount} frågor)";
}

/// <summary>Settings chosen on the start screen. Difficulty null = blandat (all levels).</summary>
public sealed record GameConfig(int AnimalId, IReadOnlyList<string> PlayerNames, Difficulty? Difficulty);

public interface IGameRepository
{
    IReadOnlyList<AnimalInfo> ListAnimals();
    Animal LoadAnimal(int id);

    /// <summary>Saves the animal with its parts and spaces. Body parts that were removed are deleted together with their questions. Returns the animal id.</summary>
    int SaveAnimal(Animal animal);

    void DeleteAnimal(int id);

    IReadOnlyList<Question> GetQuestions(int animalId);

    /// <summary>Inserts questions with Id 0, updates the rest, deletes the given ids.</summary>
    void SaveQuestions(IEnumerable<Question> questions, IEnumerable<int> deletedIds);
}

public sealed class Player
{
    public Player(string name, int number)
    {
        Name = name;
        Number = number;
    }

    public string Name { get; }

    /// <summary>0-based player number (turn order and colour).</summary>
    public int Number { get; }

    /// <summary>Index of the space the player stands on. 0 = start.</summary>
    public int Position { get; internal set; }
}
