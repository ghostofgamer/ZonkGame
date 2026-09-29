using System;

namespace Zonk.Core.Dice
{
    /// <summary>
    /// Быстрый ГСЧ SplitMix64 с сидом. Одинаков на любой платформе,
    /// в отличие от System.Random, чья реализация менялась между версиями .NET.
    /// </summary>
    public sealed class SplitMixRandom : IRandom
    {
        private ulong _state;

        public SplitMixRandom(ulong seed)
        {
            _state = seed;
        }

        /// <summary>Сид из текущего времени, для партий без повтора.</summary>
        public static ulong NewSeed()
        {
            return (ulong)DateTime.UtcNow.Ticks ^ (ulong)Environment.TickCount * 0x9E3779B97F4A7C15UL;
        }

        public double NextDouble()
        {
            // 53 старших бита дают равномерное double в [0, 1).
            return (NextULong() >> 11) * (1.0 / (1UL << 53));
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            return (int)(NextDouble() * maxExclusive);
        }

        private ulong NextULong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
