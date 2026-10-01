using System.Collections.Generic;
using Zonk.Core.Dice;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;

namespace Zonk.Core.Match
{
    /// <summary>Игрок до начала партии: имя и его шесть костей.</summary>
    public sealed class PlayerSetup
    {
        public PlayerSetup(string name, IReadOnlyList<DieSpec> dice)
        {
            Name = name;
            Dice = dice;
        }

        public string Name { get; }

        /// <summary>Ровно ZonkMatch.DiceCount костей. Индекс кости = номер слота на столе.</summary>
        public IReadOnlyList<DieSpec> Dice { get; }

        /// <summary>
        /// Личные правила только этого игрока (находки «Бесконечного забега»): действуют поверх общих правил партии
        /// на очки его комбинаций (ModifyComboScore) и штраф за его Зонк (ZonkPenalty). ModifyRules у личных правил
        /// не вызывается: пороги и цель общие для всех. Пусто — как раньше.
        /// </summary>
        public IReadOnlyList<MatchModifier> Modifiers { get; set; } = System.Array.Empty<MatchModifier>();

        public static IReadOnlyList<DieSpec> StandardDice()
        {
            var dice = new DieSpec[ZonkMatch.DiceCount];
            for (var i = 0; i < dice.Length; i++)
                dice[i] = DieSpec.Standard;
            return dice;
        }
    }

    /// <summary>Всё, что нужно для партии. Одинаковые настройки и сид дают одинаковую партию.</summary>
    public sealed class MatchSettings
    {
        public RuleSet Rules = RuleSet.CreateClassic();
        public List<PlayerSetup> Players = new List<PlayerSetup>();
        public List<MatchModifier> Modifiers = new List<MatchModifier>();
        public ulong Seed = 1;
        public int FirstPlayer;
    }
}
