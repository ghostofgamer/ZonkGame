using System;
using System.Collections.Generic;
using NUnit.Framework;
using Zonk.Core.Ai;
using Zonk.Core.Dice;
using Zonk.Core.Match;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;
using Zonk.Core.Simulation;

namespace Zonk.Tests
{
    public sealed class MatchTests
    {
        /// <summary>Кость, которая всегда выпадает гранью face.</summary>
        private static DieSpec Forced(int face)
        {
            var weights = new double[6];
            weights[face - 1] = 1;
            return new DieSpec("forced_" + face, weights);
        }

        private static IReadOnlyList<DieSpec> ForcedDice(params int[] faces)
        {
            var dice = new DieSpec[faces.Length];
            for (var i = 0; i < faces.Length; i++)
                dice[i] = Forced(faces[i]);
            return dice;
        }

        private static MatchSettings TwoPlayers(IReadOnlyList<DieSpec> first, IReadOnlyList<DieSpec> second, RuleSet rules = null)
        {
            return new MatchSettings
            {
                Rules = rules ?? RuleSet.CreateClassic(),
                Players = new List<PlayerSetup>
                {
                    new PlayerSetup("A", first),
                    new PlayerSetup("B", second),
                },
                Seed = 42,
            };
        }

        [Test]
        public void ZonkSaveKeepsTheTurnUntilChargesRunOut()
        {
            // 2, 2, 3, 3, 4, 6 — ни одной комбинации: каждый бросок — Зонк.
            var settings = TwoPlayers(ForcedDice(2, 2, 3, 3, 4, 6), PlayerSetup.StandardDice());
            settings.Players[0].ZonkSaves = 2;
            var match = new ZonkMatch(settings);

            for (var charge = 1; charge >= 0; charge--)
            {
                var saved = match.Roll();
                Assert.IsTrue(saved.ZonkSaved);
                Assert.IsFalse(saved.IsZonk);
                Assert.IsNull(saved.TurnEnd);
                Assert.AreEqual(MatchPhase.AwaitingRoll, match.Phase);
                Assert.AreEqual(charge, match.Players[0].ZonkSavesLeft);
                Assert.AreEqual(0, match.CurrentPlayerIndex, "the turn goes on");
            }

            var zonk = match.Roll();
            Assert.IsTrue(zonk.IsZonk);
            Assert.AreEqual(1, match.CurrentPlayerIndex, "no charges — the turn ends");
        }

        [Test]
        public void InsuranceKeepsPartOfTheLostTurn()
        {
            // Первый бросок: единица (100), дальше без комбинаций.
            var settings = TwoPlayers(ForcedDice(1, 2, 2, 3, 3, 4), PlayerSetup.StandardDice());
            settings.Players[0].Modifiers = new MatchModifier[] { new ZonkInsuranceModifier { KeepPercent = 50 } };
            settings.Players[0].StartScore = 200;
            var match = new ZonkMatch(settings);
            Assert.AreEqual(200, match.Players[0].Score, "head start");

            match.Roll();
            match.Keep(new[] { 0 });
            var zonk = match.Roll();
            Assert.IsTrue(zonk.IsZonk);
            Assert.AreEqual(50, zonk.TurnEnd.Saved);
            Assert.AreEqual(250, match.Players[0].Score);
        }

        [Test]
        public void HotHandAndBigTurnAddToTheTurn()
        {
            // Стрит: горячие кости одной комбинацией.
            var settings = TwoPlayers(ForcedDice(1, 2, 3, 4, 5, 6), PlayerSetup.StandardDice());
            settings.Players[0].Modifiers = new MatchModifier[]
            {
                new HotDiceBonusModifier { BonusPercent = 50 },
                new BigTurnBonusModifier { Threshold = 1000, BonusPercent = 20 },
            };
            var match = new ZonkMatch(settings);
            match.Roll();
            var keep = match.Keep(new[] { 0, 1, 2, 3, 4, 5 });
            Assert.IsTrue(keep.HotDice);
            Assert.AreEqual(keep.Score.Score / 2, keep.Bonus);
            Assert.AreEqual(keep.Score.Score + keep.Bonus, match.TurnScore);

            var end = match.Bank();
            Assert.AreEqual(keep.TurnScore / 5, end.Bonus, "big turn: +20%");
            Assert.AreEqual(keep.TurnScore + end.Bonus, end.Banked);
        }

        [Test]
        public void OpponentStartsRuleGivesFirstTurnToLastPlayer()
        {
            foreach (var proposed in new[] { 0, 1 })
            {
                var settings = TwoPlayers(PlayerSetup.StandardDice(), PlayerSetup.StandardDice());
                settings.FirstPlayer = proposed;
                settings.Modifiers.Add(new OpponentStartsModifier());

                Assert.That(new ZonkMatch(settings).CurrentPlayerIndex, Is.EqualTo(1));
            }

            // Без правила — как выбрал режим.
            var plain = TwoPlayers(PlayerSetup.StandardDice(), PlayerSetup.StandardDice());
            plain.FirstPlayer = 0;
            Assert.That(new ZonkMatch(plain).CurrentPlayerIndex, Is.EqualTo(0));
            Assert.That(new OpponentStartsModifier().DescriptionKey, Is.EqualTo("rule.opponentStarts"));
        }

        [Test]
        public void ZonkPassesTurn()
        {
            var match = new ZonkMatch(TwoPlayers(ForcedDice(2, 3, 4, 6, 6, 2), PlayerSetup.StandardDice()));
            var roll = match.Roll();

            Assert.That(roll.IsZonk, Is.True);
            Assert.That(roll.TurnEnd.NextPlayer, Is.EqualTo(1));
            Assert.That(match.CurrentPlayerIndex, Is.EqualTo(1));
            Assert.That(match.Phase, Is.EqualTo(MatchPhase.AwaitingRoll));
        }

        [Test]
        public void KeepAndBankAddsScore()
        {
            var match = new ZonkMatch(TwoPlayers(ForcedDice(1, 5, 2, 3, 4, 6), PlayerSetup.StandardDice()));
            match.Roll();
            var keep = match.Keep(new[] { 0, 2, 3, 4, 1, 5 });

            Assert.That(keep.Score.Score, Is.EqualTo(1500));
            Assert.That(keep.HotDice, Is.True);
            Assert.That(match.DiceInHandCount, Is.EqualTo(6));

            var end = match.Bank();
            Assert.That(end.Banked, Is.EqualTo(1500));
            Assert.That(match.Players[0].Score, Is.EqualTo(1500));
            Assert.That(match.CurrentPlayerIndex, Is.EqualTo(1));
        }

        [Test]
        public void InvalidKeepThrows()
        {
            var match = new ZonkMatch(TwoPlayers(ForcedDice(1, 5, 2, 2, 3, 4), PlayerSetup.StandardDice()));
            match.Roll();

            Assert.That(match.EvaluateSelection(new[] { 2 }).IsValid, Is.False);
            Assert.Throws<InvalidOperationException>(() => match.Keep(new[] { 2 }));
            Assert.Throws<InvalidOperationException>(() => match.Bank());
        }

        [Test]
        public void KeptDiceAreNotRolledAgain()
        {
            var match = new ZonkMatch(TwoPlayers(ForcedDice(1, 1, 5, 5, 2, 3), PlayerSetup.StandardDice()));
            match.Roll();
            match.Keep(new[] { 0 });
            var roll = match.Roll();

            Assert.That(roll.RolledDice, Has.No.Member(0));
            Assert.That(roll.RolledDice.Count, Is.EqualTo(5));
        }

        [Test]
        public void EntryScoreBlocksSmallBank()
        {
            var rules = RuleSet.CreateClassic();
            rules.EntryScore = 500;
            var match = new ZonkMatch(TwoPlayers(ForcedDice(1, 3, 3, 4, 4, 6), PlayerSetup.StandardDice(), rules));
            match.Roll();
            match.Keep(new[] { 0 });

            Assert.That(match.CanBank, Is.False);
            Assert.That(match.CanRollAgain, Is.True);
        }

        [Test]
        public void FinalRoundGivesOpponentLastTurn()
        {
            var rules = RuleSet.CreateClassic();
            rules.TargetScore = 1000;
            var match = new ZonkMatch(TwoPlayers(ForcedDice(1, 1, 1, 2, 3, 4), ForcedDice(2, 3, 4, 6, 6, 2), rules));

            match.Roll();
            match.Keep(new[] { 0, 1, 2 });
            var end = match.Bank();

            Assert.That(end.StartedFinalRound, Is.True);
            Assert.That(match.IsFinalRound, Is.True);
            Assert.That(match.Phase, Is.Not.EqualTo(MatchPhase.Finished));

            var roll = match.Roll();
            Assert.That(roll.TurnEnd.MatchFinished, Is.True);
            Assert.That(match.Winner.Index, Is.EqualTo(0));
        }

        [Test]
        public void ThreeZonksCostPenalty()
        {
            var rules = RuleSet.CreateClassic();
            rules.ThreeZonkPenalty = 500;
            var match = new ZonkMatch(TwoPlayers(ForcedDice(2, 3, 4, 6, 6, 2), ForcedDice(2, 3, 4, 6, 6, 2), rules));
            match.Players[0].Score = 1000;

            for (var i = 0; i < 3; i++)
            {
                match.Roll(); // игрок A
                match.Roll(); // игрок B
            }

            Assert.That(match.Players[0].Score, Is.EqualTo(500));
            Assert.That(match.Players[1].Score, Is.EqualTo(0));
        }



        [Test]
        public void WorthlessFiveIsZonk()
        {
            var settings = TwoPlayers(ForcedDice(5, 2, 2, 3, 4, 4), PlayerSetup.StandardDice());
            settings.Modifiers.Add(new SingleFaceModifier { Face = 5, Multiplier = 0f });
            var roll = new ZonkMatch(settings).Roll();

            Assert.That(roll.IsZonk, Is.True);
        }
        [Test]
        public void MinBankBlocksSmallBank()
        {
            var settings = TwoPlayers(ForcedDice(1, 3, 3, 4, 4, 6), PlayerSetup.StandardDice());
            settings.Modifiers.Add(new MinBankModifier { MinBankScore = 350 });
            var match = new ZonkMatch(settings);
            match.Roll();
            match.Keep(new[] { 0 });

            Assert.That(match.CanBank, Is.False);
        }
        [Test]
        public void TargetModifierChangesRules()
        {
            var settings = TwoPlayers(PlayerSetup.StandardDice(), PlayerSetup.StandardDice());
            settings.Modifiers.Add(new TargetScoreModifier { TargetScore = 6000 });
            var match = new ZonkMatch(settings);

            Assert.That(match.Rules.TargetScore, Is.EqualTo(6000));
            Assert.That(settings.Rules.TargetScore, Is.EqualTo(4000), "Base rules must not change");
        }

        [Test]
        public void SameSeedGivesSameMatch()
        {
            var first = PlayAiMatch(123);
            var second = PlayAiMatch(123);

            Assert.That(first.Players[0].Score, Is.EqualTo(second.Players[0].Score));
            Assert.That(first.Players[1].Score, Is.EqualTo(second.Players[1].Score));
            Assert.That(first.TurnNumber, Is.EqualTo(second.TurnNumber));
        }

        [Test]
        public void AiMatchFinishesWithWinnerAtTarget()
        {
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var match = PlayAiMatch(seed);
                Assert.That(match.Phase, Is.EqualTo(MatchPhase.Finished));
                Assert.That(match.Winner.Score, Is.GreaterThanOrEqualTo(match.Rules.TargetScore));
            }
        }

        private static ZonkMatch PlayAiMatch(ulong seed)
        {
            var settings = TwoPlayers(PlayerSetup.StandardDice(), PlayerSetup.StandardDice());
            settings.Seed = seed;
            var match = new ZonkMatch(settings);
            var profiles = new[] { AiProfile.CreateDefault(), new AiProfile { Selection = new ValueSelection() } };
            MatchSimulator.PlayMatch(match, profiles, new SplitMixRandom(seed + 1000));
            return match;
        }
    }
}
