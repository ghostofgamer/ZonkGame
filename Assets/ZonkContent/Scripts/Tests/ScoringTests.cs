using NUnit.Framework;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;

namespace Zonk.Tests
{
    public sealed class ScoringTests
    {
        private ScoreCalculator _calculator;

        [SetUp]
        public void SetUp()
        {
            _calculator = new ScoreCalculator(RuleSet.CreateClassicRules());
        }

        [TestCase(new[] { 1 }, 100)]
        [TestCase(new[] { 5 }, 50)]
        [TestCase(new[] { 1, 5 }, 150)]
        [TestCase(new[] { 1, 1, 1 }, 1000)]
        [TestCase(new[] { 2, 2, 2 }, 200)]
        [TestCase(new[] { 6, 6, 6 }, 600)]
        [TestCase(new[] { 2, 2, 2, 2 }, 400)]
        [TestCase(new[] { 2, 2, 2, 2, 2 }, 800)]
        [TestCase(new[] { 2, 2, 2, 2, 2, 2 }, 1600)]
        [TestCase(new[] { 1, 1, 1, 1 }, 2000)]
        [TestCase(new[] { 1, 1, 1, 5 }, 1050)]
        [TestCase(new[] { 1, 2, 3, 4, 5, 6 }, 1500)]
        [TestCase(new[] { 1, 2, 3, 4, 5 }, 500)]
        [TestCase(new[] { 2, 3, 4, 5, 6 }, 750)]
        [TestCase(new[] { 1, 2, 3, 4, 5, 5 }, 550)]
        [TestCase(new[] { 2, 2, 3, 3, 4, 4 }, 750)]
        [TestCase(new[] { 1, 1, 3, 3, 6, 6 }, 750)]
        [TestCase(new[] { 1, 1, 1, 1, 5, 5 }, 2100)]
        [TestCase(new[] { 3, 3, 3, 3, 4, 4 }, 750)]
        [TestCase(new[] { 5, 5, 5, 1, 1, 1 }, 1500)]
        public void ValidSelectionScores(int[] faces, int expected)
        {
            var result = _calculator.EvaluateFaces(faces);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Score, Is.EqualTo(expected));
        }

        [TestCase(new[] { 2 })]
        [TestCase(new[] { 2, 2 })]
        [TestCase(new[] { 1, 2 })]
        [TestCase(new[] { 2, 2, 2, 3 })]
        [TestCase(new int[0])]
        public void InvalidSelectionIsRejected(int[] faces)
        {
            Assert.That(_calculator.EvaluateFaces(faces).IsValid, Is.False);
        }

        [TestCase(new[] { 2, 3, 4, 6, 6, 2 }, false)]
        [TestCase(new[] { 2, 3, 3, 4, 6, 6 }, false)]
        [TestCase(new[] { 2, 3, 4, 5, 6, 6 }, true)]
        [TestCase(new[] { 2, 2, 3, 3, 4, 4 }, true)]
        [TestCase(new[] { 4 }, false)]
        [TestCase(new[] { 5 }, true)]
        public void DetectsAnyScore(int[] faces, bool expected)
        {
            Assert.That(_calculator.HasAnyScore(faces), Is.EqualTo(expected));
        }

        [Test]
        public void ModifierDoublesTriples()
        {
            var calculator = new ScoreCalculator(RuleSet.CreateClassicRules(), new MatchModifier[]
            {
                new ComboMultiplierModifier { Category = ComboCategory.OfAKind, Multiplier = 2f },
            });

            Assert.That(calculator.EvaluateFaces(new[] { 3, 3, 3, 1 }).Score, Is.EqualTo(700));
        }


        [Test]
        public void SingleFiveWorthNothingButTriplesStay()
        {
            var calculator = new ScoreCalculator(RuleSet.CreateClassicRules(), new MatchModifier[]
            {
                new SingleFaceModifier { Face = 5, Multiplier = 0f },
            });

            Assert.That(calculator.EvaluateFaces(new[] { 1, 5 }).Score, Is.EqualTo(100));
            Assert.That(calculator.EvaluateFaces(new[] { 5, 5, 5 }).Score, Is.EqualTo(500));
            Assert.That(calculator.EvaluateFaces(new[] { 5 }).IsValid, Is.False);
        }
        [Test]
        public void BreakdownListsCombos()
        {
            var result = _calculator.EvaluateFaces(new[] { 1, 1, 1, 5 });
            Assert.That(result.Combos.Count, Is.EqualTo(2));
        }
    }
}
