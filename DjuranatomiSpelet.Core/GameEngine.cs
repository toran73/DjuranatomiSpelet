// Djuranatomi Spelet
// Copyright (c) 2026 Torbjörn Nilsson och DSS Maja Blomqvist
// SPDX-License-Identifier: EUPL-1.2

namespace DjuranatomiSpelet.Core;

public enum TurnPhase
{
    /// <summary>Waiting for the teacher to roll the die for the current player.</summary>
    WaitingForRoll,
    /// <summary>Die rolled; the UI animates the token. Call <see cref="GameEngine.Arrive"/> when done.</summary>
    Moving,
    /// <summary>Question shown; teacher reveals the answer.</summary>
    ShowingQuestion,
    /// <summary>Answer shown; teacher clicks correct or wrong.</summary>
    ShowingAnswer,
    GameOver
}

public sealed record MoveResult(Player Player, int Roll, int From, int To);

/// <param name="MovedBackTo">Space the player was sent back to on a wrong answer (null when correct).</param>
public sealed record JudgeResult(Player Player, bool Correct, int Space, int? MovedBackTo, bool Won);

/// <summary>
/// Rules:
/// - Players take turns rolling one die and move forward along the path (never past the goal).
/// - They get a question for the body part of the space they land on.
/// - Correct: stay on the new space. Wrong: go back at most two steps
///   (only one if the last move was one step).
/// - The first player to land on the goal and answer correctly wins. No points.
/// </summary>
public sealed class GameEngine
{
    /// <summary>Maximum number of steps a player is sent back after a wrong answer.</summary>
    public const int MaxStepsBack = 2;

    private readonly Random _rng;
    private readonly QuestionDeck _deck;
    private readonly List<Player> _players;

    public GameEngine(IReadOnlyList<string> playerNames, Animal animal, Difficulty? difficulty,
        IEnumerable<Question> questions, Random? rng = null)
    {
        if (playerNames.Count == 0)
            throw new ArgumentException("Minst en spelare behövs.", nameof(playerNames));
        var problem = animal.Validate();
        if (problem is not null)
            throw new ArgumentException(problem, nameof(animal));

        _rng = rng ?? new Random();
        _players = playerNames.Select((n, i) => new Player(n, i)).ToList();
        Animal = animal;
        Difficulty = difficulty;
        _deck = new QuestionDeck(questions, _rng);
    }

    public Animal Animal { get; }
    public IReadOnlyList<BoardSpace> Spaces => Animal.Spaces;
    public int GoalIndex => Animal.Spaces.Count - 1;
    public Difficulty? Difficulty { get; }
    public IReadOnlyList<Player> Players => _players;
    public int QuestionCount => _deck.Count;

    public int CurrentIndex { get; private set; }
    public Player CurrentPlayer => _players[CurrentIndex];
    public TurnPhase Phase { get; private set; } = TurnPhase.WaitingForRoll;

    public int LastRoll { get; private set; }
    public int TurnStartSpace { get; private set; }
    public int TargetSpace { get; private set; }
    public BoardSpace CurrentSpace => Animal.Spaces[TargetSpace];
    public Question? CurrentQuestion { get; private set; }

    /// <summary>True when the last <see cref="Arrive"/> was on an empty space (no question, turn already passed on).</summary>
    public bool LandedOnEmpty { get; private set; }
    public Player? Winner { get; private set; }

    public MoveResult Roll()
    {
        Require(TurnPhase.WaitingForRoll);
        var player = CurrentPlayer;
        LastRoll = _rng.Next(1, 7);
        TurnStartSpace = player.Position;
        TargetSpace = Math.Min(TurnStartSpace + LastRoll, GoalIndex);
        player.Position = TargetSpace;
        CurrentQuestion = null;
        Phase = TurnPhase.Moving;
        return new MoveResult(player, LastRoll, TurnStartSpace, TargetSpace);
    }

    /// <summary>Call when the move animation is done. Draws the question for the space landed on.</summary>
    public Question? Arrive()
    {
        Require(TurnPhase.Moving);
        LandedOnEmpty = CurrentSpace.Part is null;
        if (LandedOnEmpty)
        {
            // Empty space: no question, the turn passes on immediately.
            NextPlayer();
            return null;
        }
        CurrentQuestion = _deck.Draw(CurrentSpace.Part!.Id, Difficulty);
        Phase = TurnPhase.ShowingQuestion;
        return CurrentQuestion;
    }

    /// <summary>Teacher wants a different question.</summary>
    public Question? DrawAnotherQuestion()
    {
        Require(TurnPhase.ShowingQuestion, TurnPhase.ShowingAnswer);
        CurrentQuestion = _deck.Draw(CurrentSpace.Part!.Id, Difficulty);
        Phase = TurnPhase.ShowingQuestion;
        return CurrentQuestion;
    }

    public void RevealAnswer()
    {
        Require(TurnPhase.ShowingQuestion);
        Phase = TurnPhase.ShowingAnswer;
    }

    public JudgeResult Judge(bool correct)
    {
        Require(TurnPhase.ShowingQuestion, TurnPhase.ShowingAnswer);
        var player = CurrentPlayer;

        if (correct)
        {
            if (TargetSpace == GoalIndex)
            {
                Winner = player;
                Phase = TurnPhase.GameOver;
                return new JudgeResult(player, true, TargetSpace, null, true);
            }
            NextPlayer();
            return new JudgeResult(player, true, TargetSpace, null, false);
        }

        // Go back at most MaxStepsBack, and never further than the steps actually moved this turn.
        var movedSteps = TargetSpace - TurnStartSpace;
        var backTo = TargetSpace - Math.Min(MaxStepsBack, movedSteps);
        player.Position = backTo;
        NextPlayer();
        return new JudgeResult(player, false, TargetSpace, backTo, false);
    }

    private void NextPlayer()
    {
        CurrentIndex = (CurrentIndex + 1) % _players.Count;
        CurrentQuestion = null;
        Phase = TurnPhase.WaitingForRoll;
    }

    private void Require(params TurnPhase[] allowed)
    {
        if (!allowed.Contains(Phase))
            throw new InvalidOperationException($"Inte tillåtet i läget {Phase}.");
    }
}
