// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

using System.Text;

namespace DjuranatomiSpelet.Core;

/// <summary>
/// Semicolon-separated CSV that opens directly in Swedish Excel.
/// Columns: Kroppsdel;Svårighet;Fråga;Svar
/// Kroppsdel must match the name of a body part on the animal (case-insensitive).
/// Svårighet: Lätt/Medel/Svår (also Easy/Medium/Hard or 1/2/3).
/// </summary>
public static class QuestionCsv
{
    public const char Separator = ';';
    public const string Header = "Kroppsdel;Svårighet;Fråga;Svar";

    public static string Write(IEnumerable<Question> questions, IEnumerable<BodyPart> parts)
    {
        var names = parts.ToDictionary(p => p.Id, p => p.Name);
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var q in questions)
        {
            var part = names.TryGetValue(q.BodyPartId, out var n) ? n : "";
            sb.AppendLine(string.Join(Separator, Escape(part), q.Difficulty.DisplayName(), Escape(q.Text), Escape(q.Answer)));
        }
        return sb.ToString();
    }

    public static (List<Question> Questions, List<string> Errors) Read(string csv, IEnumerable<BodyPart> parts)
    {
        var partList = parts.ToList();
        var questions = new List<Question>();
        var errors = new List<string>();
        var rows = ParseRows(csv.TrimStart('﻿'));

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var lineNo = i + 1;
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (i == 0 && row[0].Trim().Equals("Kroppsdel", StringComparison.OrdinalIgnoreCase)) continue;

            if (row.Count < 4)
            {
                errors.Add($"Rad {lineNo}: förväntade 4 kolumner men hittade {row.Count}.");
                continue;
            }
            var part = partList.FirstOrDefault(p => p.Name.Trim().Equals(row[0].Trim(), StringComparison.OrdinalIgnoreCase));
            if (part is null)
            {
                errors.Add($"Rad {lineNo}: kroppsdelen '{row[0]}' finns inte på det här djuret.");
                continue;
            }
            if (!DifficultyInfo.TryParse(row[1], out var difficulty))
            {
                errors.Add($"Rad {lineNo}: okänd svårighet '{row[1]}'.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(row[2]))
            {
                errors.Add($"Rad {lineNo}: frågan är tom.");
                continue;
            }
            questions.Add(new Question { BodyPartId = part.Id, Difficulty = difficulty, Text = row[2].Trim(), Answer = row[3].Trim() });
        }
        return (questions, errors);
    }

    private static string Escape(string? value)
    {
        value ??= "";
        return value.IndexOfAny(new[] { Separator, '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    /// <summary>RFC-4180-style parsing (quoted fields, doubled quotes, line breaks inside quotes).</summary>
    private static List<List<string>> ParseRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == Separator) { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\r') { /* handled by \n */ }
            else if (ch == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); }
            else field.Append(ch);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
