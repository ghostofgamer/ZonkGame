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

        /// <summary>Фора: очки на счету с начала партии (находка забега). Порог входа при этом остаётся.</summary>
        public int StartScore { get; set; }

        /// <summary>
        /// Заряды «спасения от Зонка» на партию (находка забега): Зонк не заканчивает ход — сгорает заряд, очки хода
        /// остаются, те же кости бросаются снова. Бросок решает ГСЧ партии, как обычно.
        /// </summary>
        public int ZonkSaves { get; set; }

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
