// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

namespace DjuranatomiSpelet.Core;

/// <summary>
/// Hands out questions per body part and difficulty without repeats until a pile runs out,
/// then reshuffles (never the same question twice in a row).
/// If a body part has no question at the chosen difficulty, any difficulty is used.
/// </summary>
public sealed class QuestionDeck
{
    private readonly IReadOnlyList<Question> _all;
    private readonly Random _rng;
    private readonly Dictionary<string, Queue<Question>> _piles = new();
    private readonly Dictionary<string, Question> _lastDrawn = new();

    public QuestionDeck(IEnumerable<Question> questions, Random rng)
    {
        _all = questions.Where(q => !string.IsNullOrWhiteSpace(q.Text)).ToList();
        _rng = rng;
    }

    public int Count => _all.Count;

    /// <param name="difficulty">null = any difficulty (blandat).</param>
    public Question? Draw(int bodyPartId, Difficulty? difficulty)
    {
        var key = $"{bodyPartId}|{difficulty?.ToString() ?? "Mixed"}";
        var candidates = _all.Where(q => q.BodyPartId == bodyPartId && (difficulty is null || q.Difficulty == difficulty)).ToList();
        if (candidates.Count == 0)
        {
            key = $"{bodyPartId}|Mixed";
            candidates = _all.Where(q => q.BodyPartId == bodyPartId).ToList();
        }
        if (candidates.Count == 0)
            return null;

        if (!_piles.TryGetValue(key, out var pile) || pile.Count == 0)
        {
            var shuffled = candidates.OrderBy(_ => _rng.Next()).ToList();
            if (shuffled.Count > 1 && _lastDrawn.TryGetValue(key, out var last) && ReferenceEquals(shuffled[0], last))
                (shuffled[0], shuffled[^1]) = (shuffled[^1], shuffled[0]);
            pile = new Queue<Question>(shuffled);
            _piles[key] = pile;
        }

        var q = pile.Dequeue();
        _lastDrawn[key] = q;
        return q;
    }
}
