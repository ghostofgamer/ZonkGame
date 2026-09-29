using System.Collections.Generic;

namespace Zonk.Core.Rules
{
    /// <summary>
    /// Правила партии. Собирается из ассета RuleSetConfig, модификаторы босса могут изменить копию.
    /// </summary>
    public sealed class RuleSet
    {
        /// <summary>Сколько очков нужно набрать для победы.</summary>
        public int TargetScore = 4000;

        /// <summary>Минимум очков за ход, чтобы впервые записать очки. 0 = без порога.</summary>
        public int EntryScore;

        /// <summary>Минимум очков хода, чтобы их забрать. 0 = без порога.</summary>
        public int MinBankScore;

        /// <summary>Штраф за три Зонка подряд. 0 = без штрафа.</summary>
        public int ThreeZonkPenalty;

        /// <summary>Все шесть костей отложены: игрок снова бросает все шесть.</summary>
        public bool HotDice = true;

        /// <summary>Когда кто-то дошёл до цели, остальные делают по последнему ходу.</summary>
        public bool FinalRound = true;

        public List<ScoringRule> Rules = new List<ScoringRule>();

        public RuleSet Clone()
        {
            return new RuleSet
            {
                TargetScore = TargetScore,
                EntryScore = EntryScore,
                MinBankScore = MinBankScore,
                ThreeZonkPenalty = ThreeZonkPenalty,
                HotDice = HotDice,
                FinalRound = FinalRound,
                Rules = new List<ScoringRule>(Rules),
            };
        }

        /// <summary>Классический Зонк: 1 = 100, 5 = 50, тройки, стриты, три пары. Цель 4000.</summary>
        public static RuleSet CreateClassic()
        {
            return new RuleSet
            {
                TargetScore = 4000,
                Rules = CreateClassicRules(),
            };
        }

        public static List<ScoringRule> CreateClassicRules()
        {
            return new List<ScoringRule>
            {
                new SingleDieRule { Face = 1, Points = 100 },
                new SingleDieRule { Face = 5, Points = 50 },
                new OfAKindRule(),
                new StraightRule { From = 1, To = 6, Points = 1500 },
                new StraightRule { From = 1, To = 5, Points = 500 },
                new StraightRule { From = 2, To = 6, Points = 750 },
                new ThreePairsRule(),
            };
        }
    }
}
