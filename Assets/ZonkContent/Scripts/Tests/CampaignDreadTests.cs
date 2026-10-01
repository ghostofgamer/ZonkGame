using System;
using System.Collections.Generic;
using System.Threading;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;
using Zonk.Progress;
using Zonk.UI;
using Object = UnityEngine.Object;

namespace Zonk.Tests
{
    /// <summary>Грозные версии боссов: открытие звёздами, не ломают открытие глав; тексты правил из самих правил.</summary>
    public sealed class CampaignDreadTests
    {
        private sealed class MemorySaves : ISaveStore
        {
            private readonly Dictionary<string, object> _sections = new Dictionary<string, object>();

            public bool IsLoaded => true;
            public bool IsCloudWriteBlocked => false;
            public UniTask LoadAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask WaitLoadedAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

            public T Get<T>(string key) where T : class, new()
            {
                if (!_sections.TryGetValue(key, out var section))
                    _sections[key] = section = new T();
                return (T)section;
            }

            public int GetVersion(string key) => 1;
            public void RequestSave() { }
            public UniTask SaveNowAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
        }

        private readonly List<Object> _created = new List<Object>();
        private ChapterConfig _chapter1;
        private ChapterConfig _chapter2;
        private OpponentConfig _first;
        private OpponentConfig _boss1;
        private OpponentConfig _dread1;
        private OpponentConfig _boss2;
        private CampaignProgress _progress;

        private T Create<T>(string id) where T : ContentConfig
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.Id = id;
            asset.name = id;
            _created.Add(asset);
            return asset;
        }

        private OpponentConfig Opponent(string id, bool boss, int conditions)
        {
            var opponent = Create<OpponentConfig>(id);
            opponent.IsBoss = boss;
            for (var i = 0; i < conditions; i++)
                opponent.StarConditions.Add(new HotDiceStar());
            return opponent;
        }

        [SetUp]
        public void SetUp()
        {
            _first = Opponent("first", false, 2);
            _boss1 = Opponent("boss1", true, 2);
            _dread1 = Opponent("dread1", true, 2);
            _dread1.DreadOf = _boss1;
            _dread1.DreadUnlockStars = 3;
            _boss2 = Opponent("boss2", true, 2);

            _chapter1 = Create<ChapterConfig>("chapter1");
            _chapter1.Order = 1;
            _chapter1.Opponents = new List<OpponentConfig> { _first, _boss1 };
            _chapter1.DreadBosses = new List<OpponentConfig> { _dread1 };
            _chapter2 = Create<ChapterConfig>("chapter2");
            _chapter2.Order = 2;
            _chapter2.Opponents = new List<OpponentConfig> { _boss2 };

            var database = ScriptableObject.CreateInstance<ContentDatabase>();
            _created.Add(database);
            database.EditorSetItems(new List<ContentConfig> { _first, _boss1, _dread1, _boss2, _chapter1, _chapter2 });
            _progress = new CampaignProgress(new MemorySaves(), database);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);
            _created.Clear();
        }

        [Test]
        public void DreadIsLockedUntilBossHasAllStars()
        {
            Assert.AreEqual(OpponentState.Locked, _progress.GetState(_chapter1, _dread1));

            _progress.MarkBeaten(_first);
            _progress.MarkBeaten(_boss1);
            _progress.AddStars(_boss1, 0b011);
            Assert.AreEqual(OpponentState.Locked, _progress.GetState(_chapter1, _dread1), "2 of 3 stars — still locked");

            _progress.AddStars(_boss1, 0b100);
            Assert.AreEqual(OpponentState.Available, _progress.GetState(_chapter1, _dread1));

            _progress.MarkBeaten(_dread1);
            Assert.AreEqual(OpponentState.Beaten, _progress.GetState(_chapter1, _dread1));
        }

        [Test]
        public void DreadDoesNotBlockNextChapter()
        {
            _progress.MarkBeaten(_first);
            _progress.MarkBeaten(_boss1);

            Assert.IsTrue(_progress.IsChapterUnlocked(_chapter2), "chapter 2 opens after the regular boss");
            Assert.AreEqual(_chapter2, _progress.CurrentChapter, "unbeaten dread boss does not hold the current chapter");
        }

        [Test]
        public void OpponentOutsideChapterIsLocked()
        {
            Assert.AreEqual(OpponentState.Locked, _progress.GetState(_chapter1, _boss2));
        }

        [Test]
        public void TotalStarsIncludeDread()
        {
            _progress.AddStars(_boss1, 0b111);
            _progress.AddStars(_dread1, 0b011);
            Assert.AreEqual(5, _progress.TotalStars);
        }

        [Test]
        public void RequiredStarsNeverExceedWhatBossCanGive()
        {
            _boss1.StarConditions.Clear();
            _dread1.DreadUnlockStars = 3;
            Assert.AreEqual(1, _dread1.DreadStarsRequired, "boss with only the win star — one star is enough");

            _progress.AddStars(_boss1, 0b001);
            Assert.AreEqual(OpponentState.Available, _progress.GetState(_chapter1, _dread1));
        }

        [Test]
        public void EveryRuleDescribesItselfWithItsNumbers()
        {
            // Текст — ключ и значения через «|»: проверяем, что у каждого правила есть описание и числа из правила.
            string Localize(string key) => key.StartsWith("rule.cat.") || key.StartsWith("rule.face.") ? key : key + "|{0}|{1}";

            var rules = new MatchModifier[]
            {
                new ComboMultiplierModifier { Category = ComboCategory.Straight, Multiplier = 2f },
                new TargetScoreModifier { TargetScore = 6000 },
                new EntryScoreModifier { EntryScore = 500 },
                new ZonkPenaltyModifier { Penalty = 200 },
                new ThreeZonkPenaltyModifier { Penalty = 500 },
                new SingleFaceModifier { Face = 5, Multiplier = 0f },
                new SingleFaceModifier { Face = 1, Multiplier = 0.5f },
                new MinBankModifier { MinBankScore = 350 },
            };

            var expected = new[]
            {
                "rule.comboMultiplier|2|rule.cat.straight", "rule.target|6000|", "rule.entry|500|", "rule.zonkPenalty|200|",
                "rule.threeZonks|500|", "rule.singleFaceZero|rule.face.5|", "rule.singleFace|0,5|rule.face.1", "rule.minBank|350|",
            };

            for (var i = 0; i < rules.Length; i++)
                Assert.AreEqual(expected[i], RuleTexts.Describe(rules[i], Localize, "ru"), rules[i].GetType().Name);

            Assert.AreEqual("rule.singleFace|0.5|rule.face.1", RuleTexts.Describe(rules[6], Localize, "en"), "English uses a dot");
        }
    }
}
