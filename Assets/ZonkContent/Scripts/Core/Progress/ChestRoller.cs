using System.Collections.Generic;
using Zonk.Core.Dice;

namespace Zonk.Core.Progress
{
    /// <summary>Строка таблицы сундука: монеты или вещь заданной редкости, вес (шанс относительно остальных).</summary>
    public struct ChestDropRule
    {
        /// <summary>Вещь (true) или монеты (false).</summary>
        public bool Item;

        /// <summary>Редкость вещи: 0 — обычная, 1 — редкая, 2 — легендарная.</summary>
        public int Rarity;

        public double Weight;

        /// <summary>Монеты: сколько (от и до). У вещи — замена, если все вещи этой редкости уже открыты.</summary>
        public int MinCoins;
        public int MaxCoins;
    }

    /// <summary>Что выпало: монеты или вещь (номер среди ещё не открытых вещей этой редкости).</summary>
    public struct ChestRoll
    {
        public bool IsItem;
        public int Rarity;
        public int ItemIndex;
        public int Coins;

        /// <summary>Легендарка по счётчику невезения.</summary>
        public bool Pity;
    }

    /// <summary>
    /// Розыгрыш сундука. Вещь выпадает только из ещё не открытых: если вещей этой редкости не осталось, вместо неё
    /// монеты. Счётчик невезения: после pityLimit сундуков подряд без легендарки следующая — легендарка (если она есть).
    /// Чистый C#: проверяется тестами и годится для сервера.
    /// </summary>
    public static class ChestRoller
    {
        public const int Legendary = 2;

        /// <param name="available">Сколько не открытых вещей каждой редкости (индекс — редкость).</param>
        /// <param name="sinceLegendary">Сколько сундуков открыто подряд без легендарки.</param>
        /// <param name="pityLimit">Гарантия легендарки на этом сундуке подряд; 0 — без гарантии.</param>
        public static ChestRoll Roll(IRandom random, IReadOnlyList<ChestDropRule> rules, IReadOnlyList<int> available,
            int sinceLegendary, int pityLimit)
        {
            if (pityLimit > 0 && sinceLegendary + 1 >= pityLimit && Available(available, Legendary) > 0)
                return new ChestRoll { IsItem = true, Rarity = Legendary, ItemIndex = random.Next(Available(available, Legendary)), Pity = true };

            var total = 0.0;
            for (var i = 0; i < rules.Count; i++)
                total += rules[i].Weight > 0 ? rules[i].Weight : 0;
            if (total <= 0)
                return new ChestRoll();

            var pick = random.NextDouble() * total;
            var rule = rules[rules.Count - 1];
            for (var i = 0; i < rules.Count; i++)
            {
                var weight = rules[i].Weight > 0 ? rules[i].Weight : 0;
                if (pick < weight)
                {
                    rule = rules[i];
                    break;
                }

                pick -= weight;
            }

            var left = Available(available, rule.Rarity);
            if (rule.Item && left > 0)
                return new ChestRoll { IsItem = true, Rarity = rule.Rarity, ItemIndex = random.Next(left) };

            var min = rule.MinCoins < 0 ? 0 : rule.MinCoins;
            var max = rule.MaxCoins < min ? min : rule.MaxCoins;
            return new ChestRoll { Coins = min + random.Next(max - min + 1) };
        }

        private static int Available(IReadOnlyList<int> available, int rarity)
        {
            return available != null && rarity >= 0 && rarity < available.Count ? available[rarity] : 0;
        }
    }
}
