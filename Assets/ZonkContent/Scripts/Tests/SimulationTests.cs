using System.Collections.Generic;
using NUnit.Framework;
using Zonk.Core.Ai;
using Zonk.Core.Dice;
using Zonk.Core.Match;
using Zonk.Core.Rules;
using Zonk.Core.Simulation;

namespace Zonk.Tests
{
    public sealed class SimulationTests
    {
        private static MatchSettings Settings(IReadOnlyList<DieSpec> first, IReadOnlyList<DieSpec> second)
        {
            return new MatchSettings
            {
                Rules = RuleSet.CreateClassic(),
                Players = new List<PlayerSetup> { new PlayerSetup("A", first), new PlayerSetup("B", second) },
            };
        }

        [Test]
        public void MirrorMatchIsFair()
        {
            var profile = AiProfile.CreateDefault();
            var result = MatchSimulator.Run(
                Settings(PlayerSetup.StandardDice(), PlayerSetup.StandardDice()),
                new[] { profile, profile }, 4000, 7);

            TestContext.WriteLine(result);
            Assert.That(result.WinRate(0), Is.InRange(0.46, 0.54));
        }

        [Test]
        public void LuckyDiceWinMore()
        {
            // Единицы выпадают вдвое чаще: заметно сильнее обычных, симулятор это видит.
            var lucky = new DieSpec("lucky", new double[] { 2, 1, 1, 1, 1, 1 });
            var profile = AiProfile.CreateDefault();
            var result = MatchSimulator.Run(
                Settings(MatchSimulator.Uniform(lucky), PlayerSetup.StandardDice()),
                new[] { profile, profile }, 2000, 11);

            TestContext.WriteLine(result);
            Assert.That(result.WinRate(0), Is.GreaterThan(0.6));
        }

        [Test]
        public void ValueSelectionIsNotWorseThanGreedy()
        {
            var greedy = AiProfile.CreateDefault();
            var smart = new AiProfile { Selection = new ValueSelection { DieValue = 60 } };
            var result = MatchSimulator.Run(
                Settings(PlayerSetup.StandardDice(), PlayerSetup.StandardDice()),
                new[] { smart, greedy }, 4000, 3);

            TestContext.WriteLine(result);
            Assert.That(result.WinRate(0), Is.GreaterThan(0.47));
        }
    }
}
