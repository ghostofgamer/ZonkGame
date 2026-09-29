using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Zonk.Core.Match;
using Zonk.Core.Progress;
using Zonk.Core.Rules;

namespace Zonk.Tests
{
    public sealed class ProgressTests
    {
        [Test]
        public void DayChangesAtLocalMidnight()
        {
            var evening = new DateTime(2026, 9, 29, 23, 59, 0);
            var night = new DateTime(2026, 9, 30, 0, 1, 0);
            Assert.AreEqual(QuestCalendar.DayIndex(evening) + 1, QuestCalendar.DayIndex(night));
            Assert.AreEqual(TimeSpan.FromMinutes(1), QuestCalendar.TimeToNextDay(evening));
        }

        [Test]
        public void WeekChangesOnMonday()
        {
            var sunday = new DateTime(2026, 10, 4, 22, 0, 0);
            var monday = new DateTime(2026, 10, 5, 0, 0, 0);
            var tuesday = new DateTime(2026, 10, 6, 12, 0, 0);
            Assert.AreEqual(DayOfWeek.Sunday, sunday.DayOfWeek);
            Assert.AreEqual(QuestCalendar.WeekIndex(sunday) + 1, QuestCalendar.WeekIndex(monday));
            Assert.AreEqual(QuestCalendar.WeekIndex(monday), QuestCalendar.WeekIndex(tuesday));
            Assert.AreEqual(TimeSpan.FromHours(2), QuestCalendar.TimeToNextWeek(sunday));
        }

        [Test]
        public void PickIsStableForDayAndHasNoRepeats()
        {
            var weights = new double[] { 1, 1, 2, 0, 1, 3 };
            var first = QuestPicker.Pick(weights, 3, QuestPicker.Seed(9400, 1));
            var again = QuestPicker.Pick(weights, 3, QuestPicker.Seed(9400, 1));
            CollectionAssert.AreEqual(first, again);
            Assert.AreEqual(3, first.Distinct().Count());
            CollectionAssert.DoesNotContain(first, 3);
        }

        [Test]
        public void PickRespectsExcludeAndSmallPool()
        {
            var weights = new double[] { 1, 1, 1 };
            var picked = QuestPicker.Pick(weights, 5, 7, new HashSet<int> { 0 });
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, picked);
        }

        [Test]
        public void GuaranteedQuestIsAlwaysInSet()
        {
            var weights = new double[] { 1, 1, 1, 1, 1, 1 };
            var guaranteed = new[] { false, false, false, false, true, false };
            for (var day = 0; day < 200; day++)
            {
                var set = QuestPicker.PickSet(weights, guaranteed, null, 3, QuestPicker.Seed(day, 1));
                Assert.AreEqual(3, set.Count);
                Assert.AreEqual(4, set[0]);
                Assert.AreEqual(3, set.Distinct().Count());
            }
        }

        [Test]
        public void UnavailableGuaranteedQuestIsSkipped()
        {
            var weights = new double[] { 1, 0, 1, 1 };
            var guaranteed = new[] { false, true, false, false };
            var set = QuestPicker.PickSet(weights, guaranteed, null, 3, 5);
            CollectionAssert.DoesNotContain(set, 1);
            Assert.AreEqual(3, set.Count);
        }

        [Test]
        public void OneQuestPerGroup()
        {
            var weights = new double[] { 5, 5, 5, 5, 0.1, 0.1 };
            var groups = new[] { "die", "die", "die", "die", null, null };
            for (var day = 0; day < 200; day++)
            {
                var set = QuestPicker.PickSet(weights, null, groups, 3, QuestPicker.Seed(day, 1));
                Assert.AreEqual(1, set.Count(i => groups[i] == "die"));
                Assert.AreEqual(3, set.Count);
            }
        }

        [Test]
        public void EconomyMoreActivePlayerBuysFaster()
        {
            var inputs = new EconomyInputs
            {
                StartCoins = 300, CampaignFirstWinCoins = 3000, CampaignMatches = 40, RepeatWinCoins = 30,
                DailyQuestCoins = 80, WeeklyQuestCoins = 300, MasteryPoints = new[] { 5000 }, MasteryCoins = new[] { 50 },
                CatalogCoins = 20000,
            };
            var casual = EconomyModel.Estimate(inputs, new PlayerProfile { MatchesPerDay = 6, WinRate = 0.5, DailyQuestsDone = 2 });
            var active = EconomyModel.Estimate(inputs, new PlayerProfile { MatchesPerDay = 15, WinRate = 0.55, DoubledShare = 1, DailyQuestsDone = 3, WeeklyQuestsDone = 3 });
            Assert.Greater(active.CoinsPerDay, casual.CoinsPerDay);
            Assert.Less(active.DaysToBuyAll, casual.DaysToBuyAll);
            Assert.Less(active.DaysToMasteryLevel[0], casual.DaysToMasteryLevel[0]);
        }

        [Test]
        public void TurnScoreSplitsKeepBetweenDice()
        {
            var tracker = new TurnDiceScore();
            tracker.AddKeep(new KeepOutcome { KeptDice = new[] { 0, 2, 4 }, Score = new ScoreResult(1000, Array.Empty<ScoringCombo>()) });
            tracker.AddKeep(new KeepOutcome { KeptDice = new[] { 1 }, Score = new ScoreResult(100, Array.Empty<ScoringCombo>()) });
            var result = tracker.Commit();
            CollectionAssert.AreEqual(new[] { 334, 100, 333, 0, 333, 0 }, result);
            Assert.AreEqual(0, tracker.Commit().Sum());
        }

        [Test]
        public void MasteryLevelByThresholds()
        {
            var thresholds = new[] { 1500, 6000, 20000 };
            Assert.AreEqual(0, MasteryMath.LevelFor(1499, thresholds));
            Assert.AreEqual(1, MasteryMath.LevelFor(1500, thresholds));
            Assert.AreEqual(2, MasteryMath.LevelFor(19999, thresholds));
            Assert.AreEqual(3, MasteryMath.LevelFor(50000, thresholds));
        }
    }
}
