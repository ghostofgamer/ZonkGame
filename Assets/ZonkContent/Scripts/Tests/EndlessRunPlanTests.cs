using System.Collections.Generic;
using NUnit.Framework;
using Zonk.Core.Modes;

namespace Zonk.Tests
{
    /// <summary>Расчёт этажей «Бесконечного забега»: цель, сила врага, стражи, общие правила.</summary>
    public sealed class EndlessRunPlanTests
    {
        private readonly EndlessRunPlan _plan = new EndlessRunPlan();

        [Test]
        public void TargetGrowsAndStopsAtMax()
        {
            Assert.AreEqual(1550, _plan.Target(1));
            Assert.AreEqual(2000, _plan.Target(10));
            Assert.AreEqual(3500, _plan.Target(40));
            Assert.AreEqual(3500, _plan.Target(500));
        }

        [Test]
        public void EnemyStartsSoftAndAccelerates()
        {
            Assert.AreEqual(0.7f, _plan.EnemyPower(1), 0.0001f);
            Assert.AreEqual(0.7f + 0.4f + 0.08f, _plan.EnemyPower(11), 0.0001f);
            Assert.Greater(_plan.EnemyPower(61) - _plan.EnemyPower(51), _plan.EnemyPower(11) - _plan.EnemyPower(1), "growth accelerates");
            Assert.AreEqual(0, _plan.EnemySpecialDice(7));
            Assert.AreEqual(1, _plan.EnemySpecialDice(8));
            Assert.AreEqual(6, _plan.EnemySpecialDice(200), "never more than six dice");
            Assert.AreEqual(0, _plan.AiTier(1));
            Assert.AreEqual(1, _plan.AiTier(8));
            Assert.AreEqual(2, _plan.AiTier(25));
        }

        [Test]
        public void GuardiansEveryTenthFloor()
        {
            Assert.IsFalse(_plan.IsGuardian(9));
            Assert.IsTrue(_plan.IsGuardian(10));
            Assert.IsTrue(_plan.IsGuardian(100));
        }

        [Test]
        public void RulesAppearEveryFifthFloorAndRotate()
        {
            var rules = new List<int>();
            _plan.ActiveRules(123, 4, 8, rules);
            Assert.AreEqual(0, rules.Count);

            _plan.ActiveRules(123, 5, 8, rules);
            Assert.AreEqual(1, rules.Count);

            _plan.ActiveRules(123, 15, 8, rules);
            Assert.AreEqual(3, rules.Count);
            var third = rules[2];

            _plan.ActiveRules(123, 20, 8, rules);
            Assert.AreEqual(3, rules.Count, "no more than three at once");
            Assert.AreEqual(third, rules[1], "the oldest rule leaves, the rest shift");
        }

        [Test]
        public void ActiveRulesNeverRepeatAndAreStableForTheSeed()
        {
            var first = new List<int>();
            var again = new List<int>();
            for (var floor = 1; floor <= 200; floor++)
            {
                _plan.ActiveRules(777, floor, 8, first);
                _plan.ActiveRules(777, floor, 8, again);
                CollectionAssert.AreEqual(first, again, "same seed and floor — same rules");
                CollectionAssert.AllItemsAreUnique(first, "floor " + floor);
                foreach (var index in first)
                    Assert.That(index, Is.InRange(0, 7));
            }
        }

        [Test]
        public void SmallPoolStillWorks()
        {
            var rules = new List<int>();
            _plan.ActiveRules(5, 50, 1, rules);
            Assert.AreEqual(3, rules.Count, "pool of one rule — it repeats, nothing breaks");
            _plan.ActiveRules(5, 50, 0, rules);
            Assert.AreEqual(0, rules.Count);
        }
    }
}
