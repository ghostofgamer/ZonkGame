using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Dice;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;
using Zonk.Progress;

namespace Zonk.Configs
{
    /// <summary>Тактика находки: подсказка игроку, какую сборку он собирает (цвет и подпись в окне выбора).</summary>
    public enum RunTactic
    {
        General,

        /// <summary>Рисковая: горячие кости, длинный ход, дорогие комбинации.</summary>
        Risky,

        /// <summary>Осторожная: страховка, щиты, фора, облегчение.</summary>
        Careful,

        /// <summary>Костяная: особые и гружёные кости.</summary>
        Dice,

        /// <summary>Удачливая: спасение от Зонка, талисман, второе дыхание.</summary>
        Lucky,
    }

    /// <summary>
    /// Находка «Бесконечного забега» в конфиге: ID (в сохранении, не менять), редкость (шанс и цвет), вес, сколько раз
    /// можно взять (стаки — каждый следующий раз сильнее), тактика и поведение (наследник RunPerk). Тексты:
    /// run.perk.&lt;Id&gt; — название, run.perk.&lt;Id&gt;.desc — что даёт с учётом стаков ({0}, {1}… — RunPerk.DescriptionArgs).
    /// </summary>
    [Serializable]
    public sealed class RunPerkEntry
    {
        public string Id;
        public Rarity Rarity;

        [Min(0)] public int Weight = 10;

        [Tooltip("Сколько раз можно взять за забег")]
        [Min(1)] public int MaxStacks = 1;

        public RunTactic Tactic;

        [SerializeReference, SubclassSelector]
        public RunPerk Perk;

        public string NameKey => "run.perk." + Id;
        public string DescriptionKey => "run.perk." + Id + ".desc";
    }

    /// <summary>Что знает находка о забеге. Собирается прогрессом забега (EndlessRunProgress).</summary>
    public sealed class RunPerkContext
    {
        public EndlessRunSave Data;
        public EndlessRunConfig Config;
        public ContentDatabase Content;

        /// <summary>Наибольшее число сердец (с талантами).</summary>
        public int MaxHearts;

        public int Stacks(string id)
        {
            if (Data == null || string.IsNullOrEmpty(id))
                return 0;
            for (var i = 0; i < Data.Perks.Count; i++)
            {
                if (Data.Perks[i].Id == id)
                    return Data.Perks[i].Stacks;
            }

            return 0;
        }
    }

    /// <summary>
    /// Поведение находки. Новая находка — наследник (появится в списке инспектора) и строка в EndlessRunConfig.Perks.
    /// Хуки пустые по умолчанию. Личные правила на партию — только на игрока (PlayerSetup.Modifiers, фора, заряды).
    /// </summary>
    [Serializable]
    public abstract class RunPerk
    {
        /// <summary>Можно ли предложить сейчас (кроме лимита стаков — его проверяет прогресс).</summary>
        public virtual bool IsAvailable(RunPerkContext context)
        {
            return true;
        }

        /// <summary>Значение находки при предложении (категория комбинации, ID кости); null — без значения.</summary>
        public virtual string PickValue(RunPerkContext context, IRandom random)
        {
            return null;
        }

        /// <summary>Находку взяли: разовые эффекты (сердце, щит, заряды, кость). stacks — уже с учётом взятой.</summary>
        public virtual void OnTaken(RunPerkContext context, string value, int stacks)
        {
        }

        /// <summary>Постоянное действие на этаж: личные правила игрока, фора, цель.</summary>
        public virtual void ApplyToFloor(RunPerkContext context, RunFloor floor, int stacks)
        {
        }

        public virtual float CoinMultiplier(int stacks) => 1f;
        public virtual float TokenMultiplier(int stacks) => 1f;

        /// <summary>Сколько находок больше на выбор.</summary>
        public virtual int ExtraOffers(int stacks) => 0;

        /// <summary>Бесплатных зарядов спасения на каждую партию (не копятся, сверх общего запаса).</summary>
        public virtual int FreeZonkSavesPerMatch(int stacks) => 0;

        /// <summary>Сердца кончились — одно возвращается один раз за забег.</summary>
        public virtual bool SecondWind => false;

        /// <summary>Значения для текста описания: stacks — сколько будет после взятия.</summary>
        public virtual object[] DescriptionArgs(RunPerkContext context, string value, int stacks)
        {
            return Array.Empty<object>();
        }
    }

    [Serializable]
    public sealed class HeartPerk : RunPerk
    {
        public int Hearts = 1;

        public override bool IsAvailable(RunPerkContext context) => context.Data.Hearts < context.MaxHearts;

        public override void OnTaken(RunPerkContext context, string value, int stacks)
        {
            context.Data.Hearts = Math.Min(context.MaxHearts, context.Data.Hearts + Hearts);
        }

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) =>
            new object[] { Hearts, Math.Min(context.MaxHearts, context.Data.Hearts + Hearts) };
    }

    /// <summary>Очки категории комбинаций дороже (стаки — по каждой категории свои, в Data.Combos).</summary>
    [Serializable]
    public sealed class ComboPerk : RunPerk
    {
        public float Step = 0.2f;

        public override string PickValue(RunPerkContext context, IRandom random)
        {
            var categories = EndlessRunProgress.ComboCategories;
            return categories[(int)(random.NextDouble() * categories.Length)];
        }

        public override void OnTaken(RunPerkContext context, string value, int stacks)
        {
            var combo = context.Data.Combos.Find(c => c.Category == value);
            if (combo == null)
                context.Data.Combos.Add(combo = new RunComboSave { Category = value, Multiplier = 1f });
            combo.Multiplier += Step;
        }

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks)
        {
            var combo = context.Data.Combos.Find(c => c.Category == value);
            var multiplier = (combo != null ? combo.Multiplier : 1f) + Step;
            return new object[] { Math.Round(multiplier, 2), "@rule.cat." + value };
        }
    }

    /// <summary>Особая кость забега в свободный слот.</summary>
    [Serializable]
    public sealed class SpecialDiePerk : RunPerk
    {
        public override bool IsAvailable(RunPerkContext context) => FreeSlot(context) >= 0 && Pick(context, null) != null;

        public override string PickValue(RunPerkContext context, IRandom random)
        {
            var die = Pick(context, random);
            return die != null ? die.Id : null;
        }

        public override void OnTaken(RunPerkContext context, string value, int stacks)
        {
            var slot = FreeSlot(context);
            if (slot >= 0 && !string.IsNullOrEmpty(value) && !context.Data.Dice.Contains(value))
                context.Data.Dice[slot] = value;
        }

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks)
        {
            var die = context.Content != null ? context.Content.Get<DieConfig>(value) : null;
            return new object[] { die != null ? "@" + die.NameKey : value };
        }

        internal static int FreeSlot(RunPerkContext context) => context.Data.Dice.FindIndex(string.IsNullOrEmpty);

        private static DieConfig Pick(RunPerkContext context, IRandom random)
        {
            var free = 0;
            foreach (var die in context.Config.SpecialDice)
            {
                if (die != null && !context.Data.Dice.Contains(die.Id))
                    free++;
            }

            if (free == 0)
                return null;
            var index = random != null ? (int)(random.NextDouble() * free) : 0;
            foreach (var die in context.Config.SpecialDice)
            {
                if (die == null || context.Data.Dice.Contains(die.Id))
                    continue;
                if (index-- == 0)
                    return die;
            }

            return null;
        }
    }

    /// <summary>
    /// Гружёная кость: ступени по стакам — «1 или 5» → «всегда 5» → «всегда 1». Это особые кости с весами граней
    /// (DieConfig.Weights, RunOnly): честно, бросок решает ГСЧ партии. Следующая ступень заменяет прежнюю в том же слоте.
    /// </summary>
    [Serializable]
    public sealed class LoadedDiePerk : RunPerk
    {
        [Tooltip("Ступени: кость на 1-й, 2-й, 3-й раз")]
        public List<DieConfig> Steps = new List<DieConfig>();

        public override bool IsAvailable(RunPerkContext context)
        {
            var stacks = context.Stacks(Id(context));
            if (stacks >= Steps.Count || Steps.Count == 0)
                return false;
            return stacks > 0 ? SlotOf(context, Steps[stacks - 1]) >= 0 : SpecialDiePerk.FreeSlot(context) >= 0;
        }

        public override void OnTaken(RunPerkContext context, string value, int stacks)
        {
            if (stacks < 1 || stacks > Steps.Count || Steps[stacks - 1] == null)
                return;
            var slot = stacks > 1 ? SlotOf(context, Steps[stacks - 2]) : SpecialDiePerk.FreeSlot(context);
            if (slot >= 0)
                context.Data.Dice[slot] = Steps[stacks - 1].Id;
        }

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks)
        {
            var step = Math.Max(1, Math.Min(stacks, Steps.Count));
            var die = Steps.Count > 0 ? Steps[step - 1] : null;
            return new object[] { die != null ? "@" + die.NameKey : string.Empty, step, Steps.Count };
        }

        private static int SlotOf(RunPerkContext context, DieConfig die) => die != null ? context.Data.Dice.IndexOf(die.Id) : -1;

        /// <summary>ID записи этой находки в конфиге (стаки хранятся по ней).</summary>
        private string Id(RunPerkContext context)
        {
            foreach (var entry in context.Config.Perks)
            {
                if (entry != null && entry.Perk == this)
                    return entry.Id;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class ShieldPerk : RunPerk
    {
        public int Shields = 1;

        public override void OnTaken(RunPerkContext context, string value, int stacks) => context.Data.Shields += Shields;

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) =>
            new object[] { Shields, context.Data.Shields + Shields };
    }

    /// <summary>Фора: очки на счету с начала каждой партии.</summary>
    [Serializable]
    public sealed class HeadStartPerk : RunPerk
    {
        public int Points = 150;

        public override void ApplyToFloor(RunPerkContext context, RunFloor floor, int stacks) => floor.PlayerStartScore += Points * stacks;

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) => new object[] { Points * stacks };
    }

    /// <summary>Страховка: при Зонке часть очков хода остаётся.</summary>
    [Serializable]
    public sealed class InsurancePerk : RunPerk
    {
        public int PercentPerStack = 15;
        public int MaxPercent = 60;

        public override void ApplyToFloor(RunPerkContext context, RunFloor floor, int stacks) =>
            floor.PlayerModifiers.Add(new ZonkInsuranceModifier { KeepPercent = Percent(stacks) });

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) => new object[] { Percent(stacks) };

        private int Percent(int stacks) => Math.Min(MaxPercent, PercentPerStack * stacks);
    }

    [Serializable]
    public sealed class HotHandPerk : RunPerk
    {
        public int PercentPerStack = 50;

        public override void ApplyToFloor(RunPerkContext context, RunFloor floor, int stacks) =>
            floor.PlayerModifiers.Add(new HotDiceBonusModifier { BonusPercent = PercentPerStack * stacks });

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) => new object[] { PercentPerStack * stacks };
    }

    /// <summary>Длинный ход: забранные от Threshold очки дороже.</summary>
    [Serializable]
    public sealed class BigTurnPerk : RunPerk
    {
        public int Threshold = 1000;
        public int PercentPerStack = 15;

        public override void ApplyToFloor(RunPerkContext context, RunFloor floor, int stacks) =>
            floor.PlayerModifiers.Add(new BigTurnBonusModifier { Threshold = Threshold, BonusPercent = PercentPerStack * stacks });

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) =>
            new object[] { PercentPerStack * stacks, Threshold };
    }

    /// <summary>Одиночные единицы или пятёрки дороже.</summary>
    [Serializable]
    public sealed class SingleFacePerk : RunPerk
    {
        public int Face = 1;
        public float StepPerStack = 0.3f;

        public override void ApplyToFloor(RunPerkContext context, RunFloor floor, int stacks) =>
            floor.PlayerModifiers.Add(new SingleFaceModifier { Face = Face, Multiplier = 1f + StepPerStack * stacks });

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) =>
            new object[] { Math.Round(1f + StepPerStack * stacks, 2), "@rule.face." + Face };
    }

    /// <summary>Облегчение: цель этажей ниже.</summary>
    [Serializable]
    public sealed class ReliefPerk : RunPerk
    {
        public int PercentPerStack = 8;
        public int MaxPercent = 30;

        public override void ApplyToFloor(RunPerkContext context, RunFloor floor, int stacks) =>
            floor.Target = Math.Max(500, (int)Math.Round(floor.Target * (100 - Percent(stacks)) / 100.0 / 50.0) * 50);

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) => new object[] { Percent(stacks) };

        private int Percent(int stacks) => Math.Min(MaxPercent, PercentPerStack * stacks);
    }

    /// <summary>Спасение от Зонка: заряды в общий запас забега.</summary>
    [Serializable]
    public sealed class ZonkSavePerk : RunPerk
    {
        public int Charges = 1;

        public override void OnTaken(RunPerkContext context, string value, int stacks) => context.Data.ZonkSaves += Charges;

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) =>
            new object[] { Charges, context.Data.ZonkSaves + Charges };
    }

    /// <summary>Талисман: в каждой партии бесплатный заряд спасения (не копится).</summary>
    [Serializable]
    public sealed class LuckyCharmPerk : RunPerk
    {
        public override int FreeZonkSavesPerMatch(int stacks) => stacks;

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) => new object[] { stacks };
    }

    [Serializable]
    public sealed class GoldenVeinPerk : RunPerk
    {
        public float Multiplier = 2f;

        public override float CoinMultiplier(int stacks) => stacks > 0 ? Multiplier : 1f;

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) => new object[] { Multiplier };
    }

    [Serializable]
    public sealed class TokenPursePerk : RunPerk
    {
        public int Tokens = 5;

        public override void OnTaken(RunPerkContext context, string value, int stacks) => context.Data.Tokens += Tokens;

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) => new object[] { Tokens };
    }

    [Serializable]
    public sealed class TokenHunterPerk : RunPerk
    {
        public float Multiplier = 2f;

        public override float TokenMultiplier(int stacks) => stacks > 0 ? Multiplier : 1f;

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) => new object[] { Multiplier };
    }

    [Serializable]
    public sealed class WiderChoicePerk : RunPerk
    {
        public override int ExtraOffers(int stacks) => stacks;

        public override object[] DescriptionArgs(RunPerkContext context, string value, int stacks) =>
            new object[] { context.Config.Offers + stacks };
    }

    [Serializable]
    public sealed class SecondWindPerk : RunPerk
    {
        public override bool SecondWind => true;

        public override bool IsAvailable(RunPerkContext context) => !context.Data.SecondWindUsed;
    }

    /// <summary>Ветеран: сердце, щит и заряд спасения разом.</summary>
    [Serializable]
    public sealed class VeteranPerk : RunPerk
    {
        public override void OnTaken(RunPerkContext context, string value, int stacks)
        {
            context.Data.Hearts = Math.Min(context.MaxHearts, context.Data.Hearts + 1);
            context.Data.Shields++;
            context.Data.ZonkSaves++;
        }
    }
}
