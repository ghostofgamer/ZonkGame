using System.Collections.Generic;
using NUnit.Framework;
using Zonk.Core.Dice;
using Zonk.Core.Progress;

namespace Zonk.Tests
{
    /// <summary>Сундук: шансы по таблице, только не открытые вещи, монеты вместо вещей, гарантия легендарки.</summary>
    public sealed class ChestTests
    {
        private static readonly List<ChestDropRule> Table = new List<ChestDropRule>
        {
            new ChestDropRule { Item = false, Weight = 60, MinCoins = 50, MaxCoins = 150 },
            new ChestDropRule { Item = false, Weight = 15, MinCoins = 200, MaxCoins = 400 },
            new ChestDropRule { Item = true, Rarity = 0, Weight = 18, MinCoins = 100, MaxCoins = 200 },
            new ChestDropRule { Item = true, Rarity = 1, Weight = 6, MinCoins = 200, MaxCoins = 350 },
            new ChestDropRule { Item = true, Rarity = 2, Weight = 1, MinCoins = 400, MaxCoins = 600 },
        };

        [Test]
        public void SharesFollowWeights()
        {
            var random = new SplitMixRandom(42);
            var available = new[] { 100, 100, 100 };
            var counts = new int[4];
            const int rolls = 100000;
            for (var i = 0; i < rolls; i++)
            {
                var roll = ChestRoller.Roll(random, Table, available, 0, 0);
                counts[roll.IsItem ? roll.Rarity + 1 : 0]++;
            }

            Assert.AreEqual(0.75, counts[0] / (double)rolls, 0.01);
            Assert.AreEqual(0.18, counts[1] / (double)rolls, 0.01);
            Assert.AreEqual(0.06, counts[2] / (double)rolls, 0.005);
            Assert.AreEqual(0.01, counts[3] / (double)rolls, 0.002);
        }

        [Test]
        public void EverythingOpenedGivesOnlyCoins()
        {
            var random = new SplitMixRandom(7);
            var none = new[] { 0, 0, 0 };
            for (var i = 0; i < 5000; i++)
            {
                var roll = ChestRoller.Roll(random, Table, none, 0, 150);
                Assert.IsFalse(roll.IsItem);
                Assert.GreaterOrEqual(roll.Coins, 50);
                Assert.LessOrEqual(roll.Coins, 600);
            }
        }

        [Test]
        public void ItemIndexStaysInsideAvailable()
        {
            var random = new SplitMixRandom(3);
            var available = new[] { 2, 1, 1 };
            for (var i = 0; i < 5000; i++)
            {
                var roll = ChestRoller.Roll(random, Table, available, 0, 0);
                if (roll.IsItem)
                    Assert.Less(roll.ItemIndex, available[roll.Rarity]);
            }
        }

        [Test]
        public void PityGivesLegendary()
        {
            var roll = ChestRoller.Roll(new SplitMixRandom(1), Table, new[] { 5, 5, 3 }, 149, 150);
            Assert.IsTrue(roll.IsItem);
            Assert.AreEqual(ChestRoller.Legendary, roll.Rarity);
            Assert.IsTrue(roll.Pity);

            // Нет не открытых легендарок — гарантии нет.
            var plain = ChestRoller.Roll(new SplitMixRandom(1), Table, new[] { 5, 5, 0 }, 149, 150);
            Assert.IsFalse(plain.Pity);
        }
    }
}
