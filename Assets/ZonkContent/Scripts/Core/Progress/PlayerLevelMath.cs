using System;

namespace Zonk.Core.Progress
{
    /// <summary>
    /// Уровень игрока по сумме опыта. Опыт до следующего уровня растёт линейно: first + growth × (уровень − 1),
    /// поэтому первые уровни идут быстро, а дальние — неделями. Уровень 1 — с нуля опыта.
    /// </summary>
    public static class PlayerLevelMath
    {
        /// <summary>Сколько опыта нужно с уровня level до level + 1.</summary>
        public static long XpToNext(int level, int first, int growth)
        {
            return Math.Max(1L, first + (long)growth * (Math.Max(1, level) - 1));
        }

        /// <summary>Сколько опыта всего нужно, чтобы достичь уровня level (уровень 1 — 0).</summary>
        public static long TotalXpFor(int level, int first, int growth)
        {
            var steps = (long)Math.Max(0, level - 1);
            return Math.Max(0L, first * steps + growth * steps * (steps - 1) / 2);
        }

        /// <summary>Уровень при сумме опыта xp, не выше maxLevel (0 и меньше — без предела).</summary>
        public static int LevelFor(long xp, int first, int growth, int maxLevel)
        {
            var level = 1;
            var need = 0L;
            while (maxLevel <= 0 || level < maxLevel)
            {
                need += XpToNext(level, first, growth);
                if (xp < need)
                    break;
                level++;
            }

            return level;
        }
    }
}
