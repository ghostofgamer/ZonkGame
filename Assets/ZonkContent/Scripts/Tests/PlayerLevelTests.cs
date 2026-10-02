using NUnit.Framework;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Progress;

namespace Zonk.Tests
{
    /// <summary>Уровень игрока: кривая опыта и опыт за партию.</summary>
    public sealed class PlayerLevelTests
    {
        private const int First = 200;
        private const int Growth = 60;

        [Test]
        public void CurveGrowsLinearly()
        {
            Assert.AreEqual(200, PlayerLevelMath.XpToNext(1, First, Growth));
            Assert.AreEqual(260, PlayerLevelMath.XpToNext(2, First, Growth));
            Assert.AreEqual(0, PlayerLevelMath.TotalXpFor(1, First, Growth));
            Assert.AreEqual(200, PlayerLevelMath.TotalXpFor(2, First, Growth));
            Assert.AreEqual(460, PlayerLevelMath.TotalXpFor(3, First, Growth));
        }

        [Test]
        public void LevelMatchesTotalXp()
        {
            Assert.AreEqual(1, PlayerLevelMath.LevelFor(0, First, Growth, 0));
            Assert.AreEqual(1, PlayerLevelMath.LevelFor(199, First, Growth, 0));
            Assert.AreEqual(2, PlayerLevelMath.LevelFor(200, First, Growth, 0));
            for (var level = 1; level < 80; level++)
            {
                var total = PlayerLevelMath.TotalXpFor(level, First, Growth);
                Assert.AreEqual(level, PlayerLevelMath.LevelFor(total, First, Growth, 0));
                Assert.AreEqual(level, PlayerLevelMath.LevelFor(total + PlayerLevelMath.XpToNext(level, First, Growth) - 1, First, Growth, 0));
            }
        }

        [Test]
        public void MaxLevelCapsLevel()
        {
            Assert.AreEqual(10, PlayerLevelMath.LevelFor(long.MaxValue / 4, First, Growth, 10));
        }

        [Test]
        public void MatchXpRewardsWinsLossesAndScore()
        {
            var config = ScriptableObject.CreateInstance<PlayerLevelConfig>();
            try
            {
                Assert.AreEqual(50 + 40, config.MatchXp(true, false, 4500, false));
                Assert.AreEqual(50 + 40 + 30, config.MatchXp(true, false, 4500, true));
                Assert.AreEqual(20 + 20, config.MatchXp(false, false, 2999, true));
                Assert.AreEqual(20 + 100, config.MatchXp(false, false, 50000, false));
                Assert.AreEqual(0, config.MatchXp(false, true, 9000, false));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
