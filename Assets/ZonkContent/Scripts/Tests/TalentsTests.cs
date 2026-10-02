using System.Collections.Generic;
using System.Threading;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Progress;
using Zonk.Progress;

namespace Zonk.Tests
{
    /// <summary>Таланты: очки за уровни и достижения, требования узлов, ранги, сброс, значения эффектов.</summary>
    public sealed class TalentsTests
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
        private MemorySaves _saves;
        private GameConfig _config;
        private TalentConfig _coins;
        private TalentConfig _chest;
        private TalentConfig _capstone;
        private AchievementConfig _hard;
        private Talents _talents;

        private T Create<T>(string id) where T : ContentConfig
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.Id = id;
            asset.name = id;
            _created.Add(asset);
            return asset;
        }

        [SetUp]
        public void SetUp()
        {
            _saves = new MemorySaves();
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.PlayerLevel = ScriptableObject.CreateInstance<PlayerLevelConfig>();
            _created.Add(_config);
            _created.Add(_config.PlayerLevel);

            _coins = Create<TalentConfig>("talent_coins");
            _coins.Effect = TalentEffect.CoinsOnWinPercent;
            _coins.ValuePerRank = 5f;
            _coins.MaxRank = 3;

            _chest = Create<TalentConfig>("talent_chest");
            _chest.Effect = TalentEffect.ChestWinsMinus;
            _chest.ValuePerRank = 1f;
            _chest.Requires = _coins;

            _capstone = Create<TalentConfig>("talent_capstone");
            _capstone.Effect = TalentEffect.FirstWinDoubleCoins;
            _capstone.BranchPointsRequired = 4;
            _capstone.CostPerRank = 2;

            _hard = Create<AchievementConfig>("ach_hard");
            _hard.TalentPoints = 2;

            var database = ScriptableObject.CreateInstance<ContentDatabase>();
            _created.Add(database);
            database.EditorSetItems(new List<ContentConfig> { _coins, _chest, _capstone, _hard });
            _talents = new Talents(_saves, database, _config);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);
            _created.Clear();
        }

        private void SetLevel(int level)
        {
            var levels = _config.PlayerLevel;
            _saves.Get<PlayerLevelSave>(SaveKeys.PlayerLevel).Xp = PlayerLevelMath.TotalXpFor(level, levels.FirstLevelXp, levels.LevelXpGrowth);
        }

        [Test]
        public void PointsComeFromLevelsAndHardAchievements()
        {
            Assert.AreEqual(0, _talents.TotalPoints);
            SetLevel(5);
            Assert.AreEqual(4, _talents.TotalPoints);
            _saves.Get<AchievementsSave>(SaveKeys.Achievements).Unlocked.Add("ach_hard");
            Assert.AreEqual(6, _talents.TotalPoints);
        }

        [Test]
        public void RanksNeedPointsAndRequirements()
        {
            SetLevel(3);
            Assert.IsFalse(_talents.CanRankUp(_chest), "requires talent_coins");
            Assert.IsTrue(_talents.TryRankUp(_coins));
            Assert.IsTrue(_talents.TryRankUp(_chest));
            Assert.AreEqual(0, _talents.FreePoints);
            Assert.IsFalse(_talents.TryRankUp(_coins), "no points left");
            Assert.AreEqual(5f, _talents.Value(TalentEffect.CoinsOnWinPercent));
            Assert.AreEqual(1f, _talents.Value(TalentEffect.ChestWinsMinus));
        }

        [Test]
        public void CapstoneNeedsBranchPointsAndCostsMore()
        {
            SetLevel(10);
            Assert.IsFalse(_talents.IsOpen(_capstone));
            for (var i = 0; i < 3; i++)
                _talents.TryRankUp(_coins);
            _talents.TryRankUp(_chest);
            Assert.AreEqual(4, _talents.PointsIn(TalentBranch.Merchant));
            Assert.IsTrue(_talents.TryRankUp(_capstone));
            Assert.AreEqual(6, _talents.SpentPoints);
            Assert.AreEqual(15f, _talents.Value(TalentEffect.CoinsOnWinPercent));
            Assert.IsFalse(_talents.TryRankUp(_coins), "max rank");
        }

        [Test]
        public void ResetReturnsAllPoints()
        {
            SetLevel(4);
            _talents.TryRankUp(_coins);
            _talents.TryRankUp(_coins);
            _talents.Reset();
            Assert.AreEqual(0, _talents.SpentPoints);
            Assert.AreEqual(3, _talents.FreePoints);
            Assert.AreEqual(0f, _talents.Value(TalentEffect.CoinsOnWinPercent));
        }
    }
}
