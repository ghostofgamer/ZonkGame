using System;
using System.Collections.Generic;
using Zonk.Core.Modifiers;

namespace Zonk.Core.Rules
{
    /// <summary>Результат подсчёта отложенных костей.</summary>
    public sealed class ScoreResult
    {
        public static readonly ScoreResult Invalid = new ScoreResult(-1, Array.Empty<ScoringCombo>());

        public ScoreResult(int score, IReadOnlyList<ScoringCombo> combos)
        {
            Score = score;
            Combos = combos;
        }

        /// <summary>Очки или -1, если не все кости входят в комбинации.</summary>
        public int Score { get; }

        public bool IsValid => Score > 0;

        public IReadOnlyList<ScoringCombo> Combos { get; }
    }

    /// <summary>
    /// Подсчёт очков по набору правил. Отложенные кости должны целиком разложиться на комбинации,
    /// из всех раскладок выбирается самая дорогая. Модификаторы партии применяются к каждой комбинации.
    /// Результаты кэшируются: разных наборов из шести костей меньше тысячи.
    /// </summary>
    public sealed class ScoreCalculator
    {
        private readonly IReadOnlyList<ScoringRule> _rules;
        private readonly IReadOnlyList<MatchModifier> _modifiers;
        private readonly Dictionary<int, ScoreResult> _cache = new Dictionary<int, ScoreResult>();
        private readonly List<ScoringCombo> _buffer = new List<ScoringCombo>();

        public ScoreCalculator(IReadOnlyList<ScoringRule> rules, IReadOnlyList<MatchModifier> modifiers = null)
        {
            _rules = rules;
            _modifiers = modifiers ?? Array.Empty<MatchModifier>();
        }

        /// <summary>Очки за набор граней, который игрок откладывает целиком.</summary>
        public ScoreResult EvaluateFaces(IReadOnlyList<int> faces)
        {
            return EvaluateCounts(Count(faces));
        }

        /// <summary>counts[грань] = число костей, индекс 0 не используется.</summary>
        public ScoreResult EvaluateCounts(int[] counts)
        {
            var key = Encode(counts);
            if (key == 0)
                return ScoreResult.Invalid;

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var result = Solve(counts);
            _cache[key] = result;
            return result;
        }

        /// <summary>Есть ли в броске хоть одна комбинация. Нет = Зонк.</summary>
        public bool HasAnyScore(IReadOnlyList<int> faces)
        {
            var counts = Count(faces);
            foreach (var rule in _rules)
            {
                _buffer.Clear();
                rule.FindCombos(counts, _buffer);
                // Комбинация считается, только если после правил партии она чего-то стоит:
                // при «одиночные пятёрки ничего не стоят» бросок с одной пятёркой — Зонк.
                foreach (var combo in _buffer)
                {
                    if (ApplyModifiers(combo) > 0)
                        return true;
                }


            }

            return false;
        }

        public static int[] Count(IReadOnlyList<int> faces)
        {
            var counts = new int[7];
            foreach (var face in faces)
            {
                if (face < 1 || face > 6)
                    throw new ArgumentOutOfRangeException(nameof(faces), $"Face {face} is out of 1..6");
                counts[face]++;
            }

            return counts;
        }

        private ScoreResult Solve(int[] counts)
        {
            // Кость с наименьшей гранью обязана войти в какую-то комбинацию.
            // Перебираем только комбинации с ней, остальное решаем рекурсивно: так нет повторов перестановок.
            var first = 0;
            for (var face = 1; face <= 6; face++)
            {
                if (counts[face] > 0)
                {
                    first = face;
                    break;
                }
            }

            var combos = new List<ScoringCombo>();
            foreach (var rule in _rules)
                rule.FindCombos(counts, combos);

            var bestScore = -1;
            IReadOnlyList<ScoringCombo> bestCombos = null;

            foreach (var combo in combos)
            {
                if (combo.Used[first] == 0)
                    continue;

                var rest = new int[7];
                var restEmpty = true;
                for (var face = 1; face <= 6; face++)
                {
                    rest[face] = counts[face] - combo.Used[face];
                    if (rest[face] > 0)
                        restEmpty = false;
                }

                var comboScore = ApplyModifiers(combo);
                int total;
                List<ScoringCombo> chain;

                if (restEmpty)
                {
                    total = comboScore;
                    chain = new List<ScoringCombo> { combo };
                }
                else
                {
                    var restResult = EvaluateCounts(rest);
                    if (restResult.Score < 0)
                        continue;

                    total = comboScore + restResult.Score;
                    chain = new List<ScoringCombo>(restResult.Combos.Count + 1) { combo };
                    chain.AddRange(restResult.Combos);
                }

                if (total > bestScore)
                {
                    bestScore = total;
                    bestCombos = chain;
                }
            }

            return bestCombos == null ? ScoreResult.Invalid : new ScoreResult(bestScore, bestCombos);
        }

        private int ApplyModifiers(ScoringCombo combo)
        {
            var score = combo.Score;
            foreach (var modifier in _modifiers)
                score = modifier.ModifyComboScore(combo, score);
            return score;
        }

        private static int Encode(int[] counts)
        {
            var key = 0;
            for (var face = 1; face <= 6; face++)
                key = key * 7 + counts[face];
            return key;
        }
    }
}
