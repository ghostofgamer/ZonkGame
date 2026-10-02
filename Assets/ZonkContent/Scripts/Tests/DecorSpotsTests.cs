using System;
using System.Collections.Generic;
using System.Threading;
using Base.Services.Monetization;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;
using Object = UnityEngine.Object;

namespace Zonk.Tests
{
    /// <summary>Места безделушек: открытие талантом, одна вещь — одно место, пустое основное место, старые сохранения.</summary>
    public sealed class DecorSpotsTests
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

        private sealed class FixedTalents : ITalents
        {
            public float Spots;
            public float Value(TalentEffect effect) => effect == TalentEffect.DecorSpotsExtra ? Spots : 0f;
            public int TotalPoints => 0;
            public int SpentPoints => 0;
            public int FreePoints => 0;
            public IReadOnlyList<TalentConfig> All => Array.Empty<TalentConfig>();
            public int RankOf(TalentConfig talent) => 0;
            public int PointsIn(TalentBranch branch) => 0;
            public bool IsOpen(TalentConfig talent) => false;
            public bool CanRankUp(TalentConfig talent) => false;
            public bool TryRankUp(TalentConfig talent) => false;
            public void Reset() { }
            public event Action Changed { add { } remove { } }
        }

        private readonly List<Object> _created = new List<Object>();
        private CosmeticSlotConfig _slot;
        private CosmeticItemConfig _candle;
        private CosmeticItemConfig _skull;
        private CosmeticItemConfig _rum;
        private FixedTalents _talents;
        private Loadout _loadout;

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
            _slot = Create<CosmeticSlotConfig>("decor");
            _slot.ExtraSpots = 3;
            _candle = Item("decor_candle");
            _skull = Item("decor_skull");
            _rum = Item("decor_rum");
            _slot.DefaultItem = _candle;

            var content = ScriptableObject.CreateInstance<ContentDatabase>();
            _created.Add(content);
            content.EditorSetItems(new List<ContentConfig> { _slot, _candle, _skull, _rum });

            var config = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(config);
            var saves = new MemorySaves();
            _talents = new FixedTalents();
            _loadout = new Loadout(saves, content, config, new Inventory(saves, new NoEntitlements(), content), _talents);
        }

        private CosmeticItemConfig Item(string id)
        {
            var item = Create<CosmeticItemConfig>(id);
            item.Slot = _slot;
            item.Payload = null;
            return item;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);
            _created.Clear();
        }

        [Test]
        public void ExtraSpotsOpenWithTalent()
        {
            Assert.AreEqual(4, _loadout.MaxSpots(_slot));
            Assert.AreEqual(1, _loadout.SpotCount(_slot));
            _talents.Spots = 2;
            Assert.AreEqual(3, _loadout.SpotCount(_slot));
            _talents.Spots = 10;
            Assert.AreEqual(4, _loadout.SpotCount(_slot));
        }

        [Test]
        public void MainSpotKeepsOldBehaviour()
        {
            Assert.AreEqual(_candle, _loadout.GetEquipped(_slot), "без экипировки — базовая вещь, как раньше");
            Assert.IsNull(_loadout.GetEquippedAt(_slot, 1), "дополнительное место по умолчанию пустое");
            _loadout.Equip(_skull);
            Assert.AreEqual(_skull, _loadout.GetEquippedAt(_slot, 0));
        }

        [Test]
        public void OneItemOneSpot()
        {
            _loadout.Equip(_skull);
            _loadout.EquipAt(_skull, 2);
            Assert.AreEqual(2, _loadout.SpotOf(_skull));
            Assert.IsNull(_loadout.GetEquipped(_slot), "основное место освободилось, а не вернуло базовую вещь");

            _loadout.EquipAt(_skull, 1);
            Assert.AreEqual(1, _loadout.SpotOf(_skull));
            Assert.IsNull(_loadout.GetEquippedAt(_slot, 2));

            _loadout.Equip(_skull);
            Assert.AreEqual(0, _loadout.SpotOf(_skull));
            Assert.IsNull(_loadout.GetEquippedAt(_slot, 1));
        }

        [Test]
        public void ClearSpot()
        {
            _loadout.EquipAt(_rum, 3);
            Assert.AreEqual(_rum, _loadout.GetEquippedAt(_slot, 3));
            _loadout.ClearAt(_slot, 3);
            Assert.IsNull(_loadout.GetEquippedAt(_slot, 3));
            Assert.AreEqual(-1, _loadout.SpotOf(_rum));
        }
    }
}
