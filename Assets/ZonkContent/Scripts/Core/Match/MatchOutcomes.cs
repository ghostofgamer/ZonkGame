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

        /// <summary>Сохранено страховкой при Зонке (уже на счету).</summary>
        public int Saved;

        /// <summary>Добавка за длинный ход при «забрать» (уже в Banked).</summary>
        public int Bonus;
    }

    public sealed class RollOutcome
    {
        public int Player;

        /// <summary>Индексы брошенных костей (слоты 0..5).</summary>
        public IReadOnlyList<int> RolledDice;

        /// <summary>Грани всех шести слотов после броска, 0 у отложенных.</summary>
        public IReadOnlyList<int> Faces;

        public bool IsZonk;

        /// <summary>
        /// Зонк, но сгорел заряд спасения: ход продолжается, очки хода целы, нужно бросить те же кости снова
        /// (фаза AwaitingRoll). IsZonk при этом false.
        /// </summary>
        public bool ZonkSaved;

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

        /// <summary>Добавка к очкам хода за горячие кости (личные правила; уже в TurnScore).</summary>
        public int Bonus;
    }

    /// <summary>Вариант, какие кости отложить. Для ИИ и подсказок.</summary>
    public sealed class KeepOption
    {
        private readonly List<int> _reusableDice;

        public KeepOption(IReadOnlyList<int> dice, int score, int diceLeft)
        {
            Dice = dice;
            Score = score;
            DiceLeft = diceLeft;
        }

        /// <summary>Вариант из общего буфера партии (ZonkMatch.GetKeepOptionsShared): заполняется заново.</summary>
        internal KeepOption()
        {
            _reusableDice = new List<int>(ZonkMatch.DiceCount);
            Dice = _reusableDice;
        }

        public IReadOnlyList<int> Dice { get; }
        public int Score { get; private set; }

        /// <summary>Сколько костей останется для следующего броска (с учётом горячих костей).</summary>
        public int DiceLeft { get; private set; }

        /// <summary>Заполнить вариант из буфера заново: возвращает очищенный список костей.</summary>
        internal List<int> Reuse(int score, int diceLeft)
        {
            Score = score;
            DiceLeft = diceLeft;
            _reusableDice.Clear();
            return _reusableDice;
        }
    }
}
