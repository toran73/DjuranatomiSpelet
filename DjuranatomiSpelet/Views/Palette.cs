// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using Avalonia.Media;
using DjuranatomiSpelet.Core;

namespace DjuranatomiSpelet.Views;

public static class Palette
{
    public static readonly IBrush Outline = Brush("#3B2F2F");

    /// <summary>Light grey for empty spaces (no question).</summary>
    public static readonly IBrush EmptySpace = Brush("#D9D9D9");

    /// <summary>Colours offered for new body parts (in order).</summary>
    public static readonly string[] PartColors =
    {
        "#F4A261", "#2A9D8F", "#E9C46A", "#E76F51", "#8AB17D", "#9B5DE5",
        "#4EA8DE", "#F15BB5", "#00BBF9", "#A47148", "#6A994E", "#B5179E"
    };

    private static readonly string[] PlayerColors =
    {
        "#D62828", "#1D4ED8", "#16A34A", "#F59E0B", "#9333EA", "#EC4899"
    };

    public static IBrush PlayerBrush(int playerNumber) => Brush(PlayerColors[playerNumber % PlayerColors.Length]);

    public static IBrush PartBrush(BodyPart part) => new SolidColorBrush(ParseOrGray(part.Color));

    public static string NextPartColor(IEnumerable<BodyPart> existing)
    {
        var used = existing.Select(p => p.Color.ToUpperInvariant()).ToHashSet();
        return PartColors.FirstOrDefault(c => !used.Contains(c)) ?? PartColors[used.Count % PartColors.Length];
    }

    public static string CycleColor(string current)
    {
        var i = Array.FindIndex(PartColors, c => c.Equals(current, StringComparison.OrdinalIgnoreCase));
        return PartColors[(i + 1) % PartColors.Length];
    }

    public static Color ParseOrGray(string hex) => Color.TryParse(hex, out var c) ? c : Colors.Gray;

    public static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
