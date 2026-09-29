using System.Collections.Generic;
using Zonk.Core.Dice;

namespace Zonk.Core.Progress
{
    /// <summary>
    /// Выбор заданий периода: взвешенно и без повторов. Зерно — номер дня или недели, поэтому набор на день
    /// один и тот же при любом числе перезапусков игры, и перебрать задания перезапуском нельзя.
    /// Группы: из кандидатов одной группы (например, «победите с костью …») в набор попадает не больше одного.
    /// </summary>
    public static class QuestPicker
    {
        /// <param name="weights">Вес каждого кандидата; кандидаты с весом ≤ 0 не выбираются.</param>
        /// <param name="exclude">Индексы, которые брать нельзя (уже выданные задания).</param>
        /// <param name="groups">Группа каждого кандидата; пусто или null = без группы.</param>
        /// <param name="usedGroups">Группы, уже занятые в наборе (обязательными заданиями); дополняется выбранными.</param>
        public static List<int> Pick(IReadOnlyList<double> weights, int count, ulong seed, ICollection<int> exclude = null,
            IReadOnlyList<string> groups = null, ICollection<string> usedGroups = null)
        {
            usedGroups = usedGroups ?? new HashSet<string>();
            var result = new List<int>();
            var random = new SplitMixRandom(seed);
            var pool = new List<int>();
            for (var i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0 && (exclude == null || !exclude.Contains(i)) && !IsGroupUsed(groups, i, usedGroups))
                    pool.Add(i);
            }

            while (result.Count < count && pool.Count > 0)
            {
                var total = 0.0;
                foreach (var index in pool)
                    total += weights[index];

                var roll = random.NextDouble() * total;
                var chosen = pool.Count - 1;
                for (var i = 0; i < pool.Count; i++)
                {
                    roll -= weights[pool[i]];
                    if (roll < 0)
                    {
                        chosen = i;
                        break;
                    }
                }

                var picked = pool[chosen];
                result.Add(picked);
                pool.RemoveAt(chosen);

                var group = GroupOf(groups, picked);
                if (group != null)
                {
                    usedGroups.Add(group);
                    pool.RemoveAll(i => GroupOf(groups, i) == group);
                }
            }

            return result;
        }

        /// <summary>
        /// Набор периода: сначала обязательные кандидаты (по порядку, с весом > 0, по одному из группы), затем
        /// остальные места по весу с учётом групп. Возвращает индексы кандидатов.
        /// </summary>
        public static List<int> PickSet(IReadOnlyList<double> weights, IReadOnlyList<bool> guaranteed, IReadOnlyList<string> groups,
            int count, ulong seed)
        {
            var result = new List<int>();
            var usedGroups = new HashSet<string>();
            for (var i = 0; i < weights.Count && result.Count < count; i++)
            {
                if (guaranteed == null || i >= guaranteed.Count || !guaranteed[i] || weights[i] <= 0 || IsGroupUsed(groups, i, usedGroups))
                    continue;

                result.Add(i);
                var group = GroupOf(groups, i);
                if (group != null)
                    usedGroups.Add(group);
            }

            result.AddRange(Pick(weights, count - result.Count, seed, new HashSet<int>(result), groups, usedGroups));
            return result;
        }

        /// <summary>Группа кандидата или null, если он без группы.</summary>
        private static string GroupOf(IReadOnlyList<string> groups, int index)
        {
            var group = groups != null && index < groups.Count ? groups[index] : null;
            return string.IsNullOrEmpty(group) ? null : group;
        }

        private static bool IsGroupUsed(IReadOnlyList<string> groups, int index, ICollection<string> usedGroups)
        {
            var group = GroupOf(groups, index);
            return group != null && usedGroups.Contains(group);
        }

        /// <summary>Зерно периода: разные для дня, недели и замены задания.</summary>
        public static ulong Seed(int period, int salt)
        {
            unchecked
            {
                return (ulong)period * 0x9E3779B97F4A7C15UL + (ulong)salt * 0xBF58476D1CE4E5B9UL + 1UL;
            }
        }
    }
}
