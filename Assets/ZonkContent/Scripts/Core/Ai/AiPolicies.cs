using System;
using System.Collections.Generic;
using Zonk.Core.Dice;
using Zonk.Core.Match;

namespace Zonk.Core.Ai
{
    /// <summary>
    /// Какие кости отложить. Новая тактика = новый наследник, в ассете AiProfileConfig он появится в списке.
    /// </summary>
    [Serializable]
    public abstract class AiSelectionPolicy
    {
        public abstract KeepOption Choose(AiContext context, IReadOnlyList<KeepOption> options, IRandom random);
    }

    /// <summary>Когда остановиться и забрать очки.</summary>
    [Serializable]
    public abstract class AiRiskPolicy
    {
        /// <summary>turnScore: очки хода после отложенных костей; diceLeft: сколько костей бросать дальше.</summary>
        public abstract bool ShouldBank(AiContext context, int turnScore, int diceLeft, IRandom random);
    }

    /// <summary>Жадный выбор: максимум очков сейчас, при равенстве оставить больше костей.</summary>
    [Serializable]
    public sealed class GreedySelection : AiSelectionPolicy
    {
        public override KeepOption Choose(AiContext context, IReadOnlyList<KeepOption> options, IRandom random)
        {
            KeepOption best = null;
            foreach (var option in options)
            {
                if (best == null || option.Score > best.Score ||
                    (option.Score == best.Score && option.DiceLeft > best.DiceLeft))
                {
                    best = option;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Расчётливый выбор: очки плюс ценность каждой кости, оставленной для переброса.
    /// Часто откладывает одну единицу вместо «единица + пятёрка», чтобы бросать больше костей.
    /// </summary>
    [Serializable]
    public sealed class ValueSelection : AiSelectionPolicy
    {
        /// <summary>Сколько очков стоит одна кость, оставшаяся в руке.</summary>
        public int DieValue = 60;

        public override KeepOption Choose(AiContext context, IReadOnlyList<KeepOption> options, IRandom random)
        {
            KeepOption best = null;
            var bestValue = double.MinValue;
            foreach (var option in options)
            {
                var value = option.Score + DieValue * option.DiceLeft;
                if (value > bestValue)
                {
                    bestValue = value;
                    best = option;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Порог по числу оставшихся костей: забрать, если очков хода не меньше BankAt[костей - 1].
    /// При отставании порог растёт (рискует), при лидерстве падает (осторожничает).
    /// </summary>
    [Serializable]
    public sealed class ThresholdRisk : AiRiskPolicy
    {
        /// <summary>Порог для 1..6 оставшихся костей.</summary>
        public int[] BankAt = { 300, 300, 400, 700, 2000, 100000 };

        /// <summary>Насколько поднимать порог при отставании на всю цель.</summary>
        public float BehindAggression = 0.5f;

        /// <summary>Насколько снижать порог при отрыве на всю цель.</summary>
        public float AheadCaution = 0.3f;

        /// <summary>Случайный разброс порога, ±доля. Делает соперника живее, но не жульничает.</summary>
        public float Jitter = 0.1f;

        public override bool ShouldBank(AiContext context, int turnScore, int diceLeft, IRandom random)
        {
            var index = Math.Max(1, Math.Min(6, diceLeft)) - 1;
            double threshold = BankAt[index];

            var deficit = Math.Max(-1.0, Math.Min(1.0, context.Deficit));
            threshold *= deficit > 0 ? 1 + deficit * BehindAggression : 1 + deficit * AheadCaution;
            threshold *= 1 + (random.NextDouble() * 2 - 1) * Jitter;

            return turnScore >= threshold;
        }
    }

    /// <summary>Хаотичный: порог каждый ход случайный в диапазоне.</summary>
    [Serializable]
    public sealed class RandomRisk : AiRiskPolicy
    {
        public int MinThreshold = 200;
        public int MaxThreshold = 1500;

        public override bool ShouldBank(AiContext context, int turnScore, int diceLeft, IRandom random)
        {
            var threshold = MinThreshold + random.NextDouble() * (MaxThreshold - MinThreshold);
            return turnScore >= threshold;
        }
    }
}
