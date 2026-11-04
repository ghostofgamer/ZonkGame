using System.Collections.Generic;
using Zonk.Core.Dice;

namespace Zonk.Core.Match
{
    /// <summary>Игрок в партии: счёт и статистика.</summary>
    public sealed class MatchPlayer
    {
        internal MatchPlayer(int index, PlayerSetup setup)
        {
            Index = index;
            Name = setup.Name;
            Dice = setup.Dice;
            Score = System.Math.Max(0, setup.StartScore);
            ZonkSavesLeft = System.Math.Max(0, setup.ZonkSaves);
            _scriptedRolls = setup.ScriptedRolls;
        }

        private readonly IReadOnlyList<IReadOnlyList<int>> _scriptedRolls;
        private int _scriptedRollIndex;

        /// <summary>Следующий заданный бросок обучения; null — бросает ГСЧ.</summary>
        internal IReadOnlyList<int> TakeScriptedRoll()
        {
            return _scriptedRolls != null && _scriptedRollIndex < _scriptedRolls.Count ? _scriptedRolls[_scriptedRollIndex++] : null;
        }

        /// <summary>Сколько зарядов «спасения от Зонка» осталось в этой партии.</summary>
        public int ZonkSavesLeft { get; internal set; }

        public int Index { get; }
        public string Name { get; }
        public IReadOnlyList<DieSpec> Dice { get; }

        public int Score { get; internal set; }

        /// <summary>Зонков подряд. Сбрасывается, когда игрок забирает очки.</summary>
        public int ZonkStreak { get; internal set; }

        /// <summary>Игрок уже записал первые очки (важно при пороге входа).</summary>
        public bool HasEntered { get; internal set; }

        public int TurnsPlayed { get; internal set; }
        public int ZonkCount { get; internal set; }
        public int BestTurn { get; internal set; }

        /// <summary>Сколько раз игрок получил горячие кости (все шесть принесли очки).</summary>
        public int HotDiceCount { get; internal set; }
    }
}
