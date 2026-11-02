using System;
using System.Collections.Generic;
using System.Threading;
using Base.Services.Monetization;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Progress;
using Zonk.Progress;
using Object = UnityEngine.Object;

namespace Zonk.Tests
{
    /// <summary>Награды уровней: предел опыта, вещь по рубежу, вещи пройденных уровней задним числом.</summary>
    public sealed class PlayerLevelRewardsTests
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

        private sealed class NoEntitlements : IEntitlements
        {
            public bool Has(string entitlement) => false;
            public IReadOnlyList<string> All => Array.Empty<string>();
            public event Action Changed { add { } remove { } }
            public void Grant(string entitlement) { }
            public void Revoke(string entitlement) { }
        }

        private sealed class FixedClock : IGameClock
        {
            public DateTime LocalNow => new DateTime(2026, 10, 5, 12, 0, 0);
        }

        private readonly List<Object> _created = new List<Object>();
        private MemorySaves _saves;
        private PlayerLevelConfig _levels;
        private Inventory _inventory;
        private PlayerLevel _level;

        private CosmeticItemConfig Item(string id)
        {
            var item = ScriptableObject.CreateInstance<CosmeticItemConfig>();
            item.Id = id;
            item.name = id;
            item.Price.Options.Add(new ProgressPriceOption { HintKey = "hint.level" });
            _created.Add(item);
            return item;
        }

        private void GiveAt(int level, ContentConfig item)
        {
            var milestone = _levels.MilestoneAt(level);
            if (milestone == null)
                _levels.Milestones.Add(milestone = new PlayerLevelMilestone { Level = level });
            milestone.Rewards.Add(new ContentReward { Item = item });
        }

        [SetUp]
        public void SetUp()
        {
            _saves = new MemorySaves();
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _levels = config.PlayerLevel = ScriptableObject.CreateInstance<PlayerLevelConfig>();
            _levels.MaxLevel = 5;
            _created.Add(config);
            _created.Add(_levels);

            var database = ScriptableObject.CreateInstance<ContentDatabase>();
            _created.Add(database);
            _inventory = new Inventory(_saves, new NoEntitlements(), database);
            _level = new PlayerLevel(_saves, config, new RewardGranter(new Wallet(_saves), _inventory), new FixedClock());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);
            _created.Clear();
        }

        [Test]
        public void XpStopsAtMaxLevel()
        {
            _level.AddXp(1_000_000);
            Assert.AreEqual(5, _level.Level);
            Assert.AreEqual(PlayerLevelMath.TotalXpFor(5, _levels.FirstLevelXp, _levels.LevelXpGrowth), _level.Xp);

            // Обновление подняло предел: игрок идёт с 5-го уровня, а не прыгает через несколько.
            _levels.MaxLevel = 10;
            Assert.AreEqual(5, _level.Level);
        }

        [Test]
        public void ItemLevelComesFromMilestones()
        {
            var star = Item("avatar_star");
            var other = Item("frame_other");
            GiveAt(3, star);
            Assert.AreEqual(3, _level.RewardLevelOf(star));
            Assert.AreEqual(0, _level.RewardLevelOf(other));

            // Рубеж выше предела не считается.
            GiveAt(8, other);
            Assert.AreEqual(0, _level.RewardLevelOf(other));
            Assert.AreEqual(5, _levels.LastLevel);
        }

        [Test]
        public void ItemsAddedBelowReachedLevelAreGrantedOnce()
        {
            var early = Item("avatar_early");
            GiveAt(2, early);
            _level.AddXp(PlayerLevelMath.TotalXpFor(4, _levels.FirstLevelXp, _levels.LevelXpGrowth));
            Assert.IsTrue(_inventory.IsOwned(early));
            Assert.AreEqual(0, _level.GrantMissedItems().Count);

            // Обновление поставило новую вещь на пройденный 3-й уровень и ещё одну впереди, на 5-й.
            var late = Item("avatar_late");
            var ahead = Item("avatar_ahead");
            GiveAt(3, late);
            GiveAt(5, ahead);
            var granted = _level.GrantMissedItems();
            Assert.AreEqual(1, granted.Count);
            Assert.AreSame(late, granted[0].Item);
            Assert.IsTrue(_inventory.IsOwned(late));
            Assert.IsFalse(_inventory.IsOwned(ahead));
            Assert.AreEqual(0, _level.GrantMissedItems().Count);
        }
    }
}
