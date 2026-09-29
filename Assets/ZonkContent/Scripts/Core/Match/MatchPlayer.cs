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
        }

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
    }
}
