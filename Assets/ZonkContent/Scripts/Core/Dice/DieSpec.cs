using System;
using System.Collections.Generic;

namespace Zonk.Core.Dice
{
    /// <summary>
    /// Механика одной кости: вероятность каждой грани. Внешний вид (скин) задаётся отдельно
    /// и на механику не влияет. Значения граней 1..6.
    /// </summary>
    public sealed class DieSpec
    {
        public const int FaceCount = 6;

        private readonly double[] _cumulative;

        public DieSpec(string id, IReadOnlyList<double> weights)
        {
            if (weights == null || weights.Count != FaceCount)
                throw new ArgumentException($"Die '{id}' needs exactly {FaceCount} weights");

            double total = 0;
            foreach (var weight in weights)
            {
                if (weight < 0 || double.IsNaN(weight))
                    throw new ArgumentException($"Die '{id}' has a negative weight");
                total += weight;
            }

            if (total <= 0)
                throw new ArgumentException($"Die '{id}' has zero total weight");

            Id = id;
            _cumulative = new double[FaceCount];

            double sum = 0;
            for (var i = 0; i < FaceCount; i++)
            {
                sum += weights[i] / total;
                _cumulative[i] = sum;
            }

            _cumulative[FaceCount - 1] = 1.0;
        }

        /// <summary>Обычная честная кость.</summary>
        public static readonly DieSpec Standard = new DieSpec("standard", new double[] { 1, 1, 1, 1, 1, 1 });

        public string Id { get; }

        /// <summary>Бросок: значение грани 1..6.</summary>
        public int Roll(IRandom random)
        {
            var value = random.NextDouble();
            for (var i = 0; i < FaceCount; i++)
            {
                if (value < _cumulative[i])
                    return i + 1;
            }

            return FaceCount;
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
