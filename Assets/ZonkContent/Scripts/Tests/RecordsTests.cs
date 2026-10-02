using NUnit.Framework;
using Zonk.Core.Progress;
using Zonk.Core.Rules;
using Zonk.Progress;

namespace Zonk.Tests
{
    /// <summary>Статистика: честная вероятность Зонка и виды комбинаций.</summary>
    public sealed class RecordsTests
    {
        [Test]
        public void ZonkChanceMatchesKnownFarkleOdds()
        {
            // Классические правила (с тремя парами и стритом): 1 кость — 2/3, 6 костей — около 2.3%.
            Assert.AreEqual(2.0 / 3.0, LuckMath.ZonkChance(1), 1e-9);
            Assert.AreEqual(4.0 / 9.0, LuckMath.ZonkChance(2), 1e-9);
            Assert.AreEqual(0.2778, LuckMath.ZonkChance(3), 1e-3);
            Assert.AreEqual(0.1574, LuckMath.ZonkChance(4), 1e-3);
            Assert.AreEqual(0.0772, LuckMath.ZonkChance(5), 1e-3);
            Assert.AreEqual(0.0231, LuckMath.ZonkChance(6), 1e-3);
            Assert.AreEqual(0, LuckMath.ZonkChance(0));
            Assert.AreEqual(0, LuckMath.ZonkChance(7));
        }

        [Test]
        public void ChanceDropsWithMoreDice()
        {
            for (var dice = 2; dice <= 6; dice++)
                Assert.Less(LuckMath.ZonkChance(dice), LuckMath.ZonkChance(dice - 1));
        }

        [Test]
        public void CombosAreClassified()
        {
            Assert.AreEqual((int)RecordedCombo.Straight,
                PlayerRecords.Classify(new ScoringCombo(ComboCategory.Straight, new[] { 0, 1, 1, 1, 1, 1, 1 }, 1500)));
            Assert.AreEqual((int)RecordedCombo.FourOfAKind,
                PlayerRecords.Classify(new ScoringCombo(ComboCategory.OfAKind, new[] { 0, 0, 4, 0, 0, 0, 0 }, 400)));
            Assert.AreEqual(-1, PlayerRecords.Classify(new ScoringCombo(ComboCategory.Single, new[] { 0, 1, 0, 0, 0, 0, 0 }, 100)));
        }
    }
}
