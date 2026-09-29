using System;
using System.Collections.Generic;

namespace Zonk.Core.Rules
{
    /// <summary>
    /// Правило подсчёта очков. Находит все комбинации, которые можно собрать из данных костей.
    /// Новая комбинация = новый наследник; в ассете RuleSetConfig он появится в списке выбора.
    /// Поля публичные: так Unity сериализует их через [SerializeReference] в конфиге.
    /// </summary>
    [Serializable]
    public abstract class ScoringRule
    {
        /// <summary>counts[грань] = число костей с этой гранью, индекс 0 не используется.</summary>
        public abstract void FindCombos(int[] counts, List<ScoringCombo> output);

        protected static int[] Empty()
        {
            return new int[7];
        }
    }

    /// <summary>Одиночная кость приносит очки: единица 100, пятёрка 50.</summary>
    [Serializable]
    public sealed class SingleDieRule : ScoringRule
    {
        public int Face = 1;
        public int Points = 100;

        public override void FindCombos(int[] counts, List<ScoringCombo> output)
        {
            if (Face < 1 || Face > 6 || counts[Face] == 0)
                return;

            var used = Empty();
            used[Face] = 1;
            output.Add(new ScoringCombo(ComboCategory.Single, used, Points));
        }
    }

    /// <summary>
    /// Три и больше одинаковых. Тройка стоит TriplePoints[грань-1], каждая следующая кость
    /// умножает цену на ExtraDieMultiplier: четыре единицы = 2000, пять = 4000, шесть = 8000.
    /// </summary>
    [Serializable]
    public sealed class OfAKindRule : ScoringRule
    {
        public int MinCount = 3;
        public int[] TriplePoints = { 1000, 200, 300, 400, 500, 600 };
        public int ExtraDieMultiplier = 2;

        public override void FindCombos(int[] counts, List<ScoringCombo> output)
        {
            for (var face = 1; face <= 6; face++)
            {
                for (var count = Math.Max(3, MinCount); count <= counts[face]; count++)
                {
                    var used = Empty();
                    used[face] = count;
                    output.Add(new ScoringCombo(ComboCategory.OfAKind, used, PointsFor(face, count)));
                }
            }
        }

        public int PointsFor(int face, int count)
        {
            var points = TriplePoints[face - 1];
            for (var extra = 3; extra < count; extra++)
                points *= ExtraDieMultiplier;
            return points;
        }
    }

    /// <summary>Стрит: по одной кости каждой грани от From до To. 1–6 = 1500, 1–5 = 500, 2–6 = 750.</summary>
    [Serializable]
    public sealed class StraightRule : ScoringRule
    {
        public int From = 1;
        public int To = 6;
        public int Points = 1500;

        public override void FindCombos(int[] counts, List<ScoringCombo> output)
        {
            if (From < 1 || To > 6 || From > To)
                return;

            var used = Empty();
            for (var face = From; face <= To; face++)
            {
                if (counts[face] == 0)
                    return;
                used[face] = 1;
            }

            output.Add(new ScoringCombo(ComboCategory.Straight, used, Points));
        }
    }

    /// <summary>Три пары из шести костей. Четвёрка с парой по желанию тоже считается тремя парами.</summary>
    [Serializable]
    public sealed class ThreePairsRule : ScoringRule
    {
        public int Points = 750;
        public bool FourAndPairCounts = true;

        public override void FindCombos(int[] counts, List<ScoringCombo> output)
        {
            var pairs = 0;
            var used = Empty();
            for (var face = 1; face <= 6; face++)
            {
                if (counts[face] == 2 || (FourAndPairCounts && counts[face] == 4))
                {
                    pairs += counts[face] / 2;
                    used[face] = counts[face];
                }
                else if (counts[face] == 6 && FourAndPairCounts)
                {
                    pairs += 3;
                    used[face] = 6;
                }
            }

            if (pairs == 3)
                output.Add(new ScoringCombo(ComboCategory.ThreePairs, used, Points));
        }
    }
}
