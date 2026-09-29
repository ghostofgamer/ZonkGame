using System.Collections.Generic;
using Zonk.Core.Rules;

namespace Zonk.Core.Match
{
    public enum MatchPhase
    {
        /// <summary>Начало хода: нужно бросить.</summary>
        AwaitingRoll,
        /// <summary>Кости брошены, есть комбинации: нужно отложить хотя бы одну.</summary>
        AwaitingKeep,
        /// <summary>Кости отложены: бросить оставшиеся или забрать очки.</summary>
        AwaitingDecision,
        Finished,
    }

    /// <summary>Как закончился ход: он передаётся следующему игроку сразу внутри ZonkMatch.</summary>
    public sealed class TurnEnd
    {
        public int Player;
        public bool IsZonk;
        public int Banked;
        public int ScoreLost;
        public int Penalty;
        public bool StartedFinalRound;
        public bool MatchFinished;
        public int NextPlayer;
    }

    public sealed class RollOutcome
    {
        public int Player;

        /// <summary>Индексы брошенных костей (слоты 0..5).</summary>
        public IReadOnlyList<int> RolledDice;

        /// <summary>Грани всех шести слотов после броска, 0 у отложенных.</summary>
        public IReadOnlyList<int> Faces;

        public bool IsZonk;

        /// <summary>Не null, если бросок закончил ход (Зонк).</summary>
        public TurnEnd TurnEnd;
    }

    public sealed class KeepOutcome
    {
        public int Player;
        public IReadOnlyList<int> KeptDice;
        public ScoreResult Score;
        public int TurnScore;

        /// <summary>Все кости отложены, в руку вернулись все шесть.</summary>
        public bool HotDice;
    }

    /// <summary>Вариант, какие кости отложить. Для ИИ и подсказок.</summary>
    public sealed class KeepOption
    {
        public KeepOption(IReadOnlyList<int> dice, int score, int diceLeft)
        {
            Dice = dice;
            Score = score;
            DiceLeft = diceLeft;
        }

        public IReadOnlyList<int> Dice { get; }
        public int Score { get; }

        /// <summary>Сколько костей останется для следующего броска (с учётом горячих костей).</summary>
        public int DiceLeft { get; }
    }
}
