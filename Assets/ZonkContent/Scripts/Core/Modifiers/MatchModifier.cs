using System;
using Zonk.Core.Rules;

namespace Zonk.Core.Modifiers
{
    /// <summary>
    /// Особое правило партии, обычно от босса. Действует на обоих игроков одинаково: босс не жульничает,
    /// а меняет условия. Игрок видит правило до начала партии: текст строится из самого правила (DescriptionKey).
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

        /// <summary>Сколько очков хода сохраняется при Зонке (страховка): добавляется к счёту. По умолчанию 0.</summary>
        public virtual int ZonkKeep(int turnScoreLost)
        {
            return 0;
        }

        /// <summary>Добавка к очкам хода за горячие кости: keepScore — очки отложенного, давшего горячие кости.</summary>
        public virtual int HotDiceBonus(int keepScore)
        {
            return 0;
        }

        /// <summary>Добавка к записанным очкам при «забрать» (длинный ход). turnScore — очки хода.</summary>
        public virtual int BankBonus(int turnScore)
        {
            return 0;
        }

        /// <summary>
        /// Кто ходит первым: proposed — выбор режима (жребий), playerCount — сколько игроков. По умолчанию не меняет.
        /// Вызывается один раз в начале партии, только у общих правил.
        /// </summary>
        public virtual int ChooseFirstPlayer(int proposed, int playerCount)
        {
            return proposed;
        }

        /// <summary>
        /// Как правило видит игрок: ключ текста (Texts.csv), {0}, {1}… — значения DescriptionArgs. Строка в значениях,
        /// начинающаяся с «@», — тоже ключ текста (название комбинации, грани). Текст строится из чисел правила,
        /// поэтому всегда совпадает с тем, что действует в партии. null — правило без описания.
        /// </summary>
        public virtual string DescriptionKey => null;

        /// <summary>Значения для текста правила. Вызывается редко (окна), может выделять память.</summary>
        public virtual object[] DescriptionArgs => Array.Empty<object>();
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

        public override string DescriptionKey => "rule.comboMultiplier";
        public override object[] DescriptionArgs => new object[] { Multiplier, "@rule.cat." + Category };
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

        public override string DescriptionKey => "rule.target";
        public override object[] DescriptionArgs => new object[] { TargetScore };
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

        public override string DescriptionKey => "rule.entry";
        public override object[] DescriptionArgs => new object[] { EntryScore };
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

        public override string DescriptionKey => "rule.zonkPenalty";
        public override object[] DescriptionArgs => new object[] { Penalty };
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

        public override string DescriptionKey => "rule.threeZonks";
        public override object[] DescriptionArgs => new object[] { Penalty };
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

        public override string DescriptionKey => Multiplier <= 0f ? "rule.singleFaceZero" : "rule.singleFace";

        public override object[] DescriptionArgs => Multiplier <= 0f
            ? new object[] { "@rule.face." + Face }
            : new object[] { Multiplier, "@rule.face." + Face };
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

        public override string DescriptionKey => "rule.minBank";
        public override object[] DescriptionArgs => new object[] { MinBankScore };
    }

    /// <summary>
    /// Первым ходит соперник — последний игрок в списке (в режимах против соперника местный игрок первый в списке,
    /// соперник — второй). Первый ход даёт около 56% побед при равной игре, поэтому это заметное, но честное
    /// и видимое до партии преимущество босса. Только для режимов против соперника, не для игры вдвоём.
    /// </summary>
    [Serializable]
    public sealed class OpponentStartsModifier : MatchModifier
    {
        public override int ChooseFirstPlayer(int proposed, int playerCount)
        {
            return playerCount - 1;
        }

        public override string DescriptionKey => "rule.opponentStarts";
    }

    /// <summary>Страховка: при Зонке KeepPercent% сгоревших очков хода остаются на счету. Обычно личное правило (находка).</summary>
    [Serializable]
    public sealed class ZonkInsuranceModifier : MatchModifier
    {
        public int KeepPercent = 25;

        public override int ZonkKeep(int turnScoreLost)
        {
            return turnScoreLost <= 0 ? 0 : turnScoreLost * Math.Max(0, Math.Min(100, KeepPercent)) / 100;
        }

        public override string DescriptionKey => "rule.insurance";
        public override object[] DescriptionArgs => new object[] { KeepPercent };
    }

    /// <summary>Горячая рука: за горячие кости ещё BonusPercent% очков отложенного, давшего их.</summary>
    [Serializable]
    public sealed class HotDiceBonusModifier : MatchModifier
    {
        public int BonusPercent = 50;

        public override int HotDiceBonus(int keepScore)
        {
            return keepScore <= 0 ? 0 : keepScore * Math.Max(0, BonusPercent) / 100;
        }

        public override string DescriptionKey => "rule.hotDiceBonus";
        public override object[] DescriptionArgs => new object[] { BonusPercent };
    }

    /// <summary>Длинный ход: забранные за ход очки от Threshold — ещё BonusPercent% сверху.</summary>
    [Serializable]
    public sealed class BigTurnBonusModifier : MatchModifier
    {
        public int Threshold = 1000;
        public int BonusPercent = 20;

        public override int BankBonus(int turnScore)
        {
            return turnScore < Threshold ? 0 : turnScore * Math.Max(0, BonusPercent) / 100;
        }

        public override string DescriptionKey => "rule.bigTurnBonus";
        public override object[] DescriptionArgs => new object[] { BonusPercent, Threshold };
    }
}
