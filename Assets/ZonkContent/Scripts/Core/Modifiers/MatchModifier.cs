using System;
using Zonk.Core.Rules;

namespace Zonk.Core.Modifiers
{
    /// <summary>
    /// Особое правило партии, обычно от босса. Действует на обоих игроков одинаково: босс не жульничает,
    /// а меняет условия. Игрок видит правило до начала партии (DescriptionKey в конфиге соперника).
    ///
    /// Новое правило = новый наследник с нужными переопределениями. Хуки пустые по умолчанию,
    /// поэтому добавление нового хука не ломает старые правила.
    /// </summary>
    [Serializable]
    public abstract class MatchModifier
    {
        /// <summary>Изменить правила до начала партии (цель, пороги). rules уже копия.</summary>
        public virtual void ModifyRules(RuleSet rules)
        {
        }

        /// <summary>Очки одной комбинации после базовых правил.</summary>
        public virtual int ModifyComboScore(ScoringCombo combo, int score)
        {
            return score;
        }

        /// <summary>Штраф к общему счёту за Зонк. turnScoreLost: сколько очков хода сгорело.</summary>
        public virtual int ZonkPenalty(int turnScoreLost)
        {
            return 0;
        }
    }

    /// <summary>Очки комбинаций выбранной категории умножаются. «Близнецы»: тройки вдвое дороже.</summary>
    [Serializable]
    public sealed class ComboMultiplierModifier : MatchModifier
    {
        public string Category = ComboCategory.OfAKind;
        public float Multiplier = 2f;

        public override int ModifyComboScore(ScoringCombo combo, int score)
        {
            return combo.Category == Category ? (int)Math.Round(score * Multiplier) : score;
        }
    }

    /// <summary>Своя цель партии. «Картограф»: до 6000.</summary>
    [Serializable]
    public sealed class TargetScoreModifier : MatchModifier
    {
        public int TargetScore = 6000;

        public override void ModifyRules(RuleSet rules)
        {
            rules.TargetScore = TargetScore;
        }
    }

    /// <summary>Порог входа: первые очки записываются только от этой суммы за ход.</summary>
    [Serializable]
    public sealed class EntryScoreModifier : MatchModifier
    {
        public int EntryScore = 500;

        public override void ModifyRules(RuleSet rules)
        {
            rules.EntryScore = EntryScore;
        }
    }

    /// <summary>Каждый Зонк стоит очков из общего счёта.</summary>
    [Serializable]
    public sealed class ZonkPenaltyModifier : MatchModifier
    {
        public int Penalty = 200;

        public override int ZonkPenalty(int turnScoreLost)
        {
            return Penalty;
        }
    }

    /// <summary>Штраф за три Зонка подряд.</summary>
    [Serializable]
    public sealed class ThreeZonkPenaltyModifier : MatchModifier
    {
        public int Penalty = 500;

        public override void ModifyRules(RuleSet rules)
        {
            rules.ThreeZonkPenalty = Penalty;
        }
    }

    /// <summary>
    /// Одиночная кость с гранью Face стоит иначе: Multiplier 0 — «одиночные пятёрки ничего не стоят».
    /// Тройки и стриты с этой гранью не меняются.
    /// </summary>
    [Serializable]
    public sealed class SingleFaceModifier : MatchModifier
    {
        public int Face = 5;
        public float Multiplier;

        public override int ModifyComboScore(ScoringCombo combo, int score)
        {
            if (combo.Category != ComboCategory.Single || Face < 1 || Face > 6 || combo.Used[Face] == 0)
                return score;

            return (int)Math.Round(score * Multiplier);
        }
    }

    /// <summary>Забирать очки можно только от MinBankScore за ход: «мелочь не считается».</summary>
    [Serializable]
    public sealed class MinBankModifier : MatchModifier
    {
        public int MinBankScore = 350;

        public override void ModifyRules(RuleSet rules)
        {
            rules.MinBankScore = Math.Max(rules.MinBankScore, MinBankScore);
        }
    }
}
