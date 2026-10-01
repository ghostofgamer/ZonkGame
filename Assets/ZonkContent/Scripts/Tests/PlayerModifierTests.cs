using System.Collections.Generic;
using NUnit.Framework;
using Zonk.Core.Ai;
using Zonk.Core.Match;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;
using Zonk.Core.Simulation;

namespace Zonk.Tests
{
    /// <summary>Личные правила игрока (находки «Бесконечного забега»): действуют только на его очки.</summary>
    public sealed class PlayerModifierTests
    {
        private static MatchSettings Settings(IReadOnlyList<MatchModifier> first, IReadOnlyList<MatchModifier> shared = null)
        {
            return new MatchSettings
            {
                Rules = RuleSet.CreateClassic(),
                Players = new List<PlayerSetup>
                {
                    new PlayerSetup("A", PlayerSetup.StandardDice()) { Modifiers = first },
                    new PlayerSetup("B", PlayerSetup.StandardDice()),
                },
                Modifiers = shared != null ? new List<MatchModifier>(shared) : new List<MatchModifier>(),
                Seed = 1,
            };
        }

        [Test]
        public void OwnModifierChangesOnlyOwnScore()
        {
            var fives = new SingleFaceModifier { Face = 5, Multiplier = 2f };
            var match = new ZonkMatch(Settings(new[] { fives }));
            var singleFive = new[] { 0, 0, 0, 0, 0, 1, 0 };

            Assert.AreEqual(0, match.CurrentPlayerIndex);
            Assert.AreEqual(100, match.Calculator.EvaluateCounts(singleFive).Score, "player A: five doubled");

            // Без личных правил пятёрка стоит как обычно.
            Assert.AreEqual(50, new ZonkMatch(Settings(null)).Calculator.EvaluateCounts(singleFive).Score, "no modifiers: 50");
            Assert.AreEqual(0, match.PlayerModifiers(1).Count, "player B has no own modifiers");
        }

        [Test]
        public void OwnModifierStacksOnTopOfSharedRule()
        {
            // Общее «пятёрки ничего не стоят» сильнее личного удвоения: 0 × 2 = 0, бросок с одной пятёркой — Зонк.
            var match = new ZonkMatch(Settings(new[] { new SingleFaceModifier { Face = 5, Multiplier = 2f } },
                new[] { new SingleFaceModifier { Face = 5, Multiplier = 0f } }));
            Assert.IsFalse(match.Calculator.HasAnyScore(new[] { 5, 2, 3, 3, 6, 6 }), "only a single five scores here");
        }

        [Test]
        public void SecondPlayerUsesOwnCalculatorOnHisTurn()
        {
            var match = new ZonkMatch(Settings(new[] { new ComboMultiplierModifier { Category = ComboCategory.Single, Multiplier = 3f } }));
            var singleOne = new[] { 0, 1, 0, 0, 0, 0, 0 };
            Assert.AreEqual(300, match.Calculator.EvaluateCounts(singleOne).Score);

            // Играем, пока ход не перейдёт ко второму игроку.
            var guard = 0;
            while (match.CurrentPlayerIndex == 0 && guard++ < 50)
            {
                var roll = match.Roll();
                if (roll.TurnEnd != null)
                    break;
                var options = match.GetKeepOptions();
                match.Keep(options[0].Dice);
                if (match.CanBank)
                    match.Bank();
            }

            Assert.AreEqual(1, match.CurrentPlayerIndex);
            Assert.AreEqual(100, match.Calculator.EvaluateCounts(singleOne).Score, "player B scores a single one normally");
        }

        [Test]
        public void SimulationWithPerksRunsAndPerksHelp()
        {
            var profile = AiProfile.CreateDefault();
            var perks = new MatchModifier[]
            {
                new ComboMultiplierModifier { Category = ComboCategory.OfAKind, Multiplier = 1.5f },
                new SingleFaceModifier { Face = 1, Multiplier = 1.5f },
            };
            var result = MatchSimulator.Run(Settings(perks), new[] { profile, profile }, 2000, 3);
            TestContext.WriteLine(result);
            Assert.That(result.WinRate(0), Is.GreaterThan(0.55), "perks give a clear edge");
        }
    }
}
