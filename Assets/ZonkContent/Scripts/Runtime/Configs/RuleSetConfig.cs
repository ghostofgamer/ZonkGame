using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Rules;

namespace Zonk.Configs
{
    /// <summary>Правила партии: цель, пороги и список комбинаций. Разные режимы берут разные ассеты.</summary>
    [CreateAssetMenu(menuName = "Zonk/Rule Set", fileName = "RuleSet")]
    public sealed class RuleSetConfig : ContentConfig
    {
        public int TargetScore = 4000;
        public int EntryScore;
        public int MinBankScore;
        public int ThreeZonkPenalty;
        public bool HotDice = true;
        public bool FinalRound = true;

        [SerializeReference, SubclassSelector]
        public List<ScoringRule> Rules = RuleSet.CreateClassicRules();

        public RuleSet ToRuleSet(int targetOverride = 0)
        {
            return new RuleSet
            {
                TargetScore = targetOverride > 0 ? targetOverride : TargetScore,
                EntryScore = EntryScore,
                MinBankScore = MinBankScore,
                ThreeZonkPenalty = ThreeZonkPenalty,
                HotDice = HotDice,
                FinalRound = FinalRound,
                Rules = new List<ScoringRule>(Rules),
            };
        }
    }
}
