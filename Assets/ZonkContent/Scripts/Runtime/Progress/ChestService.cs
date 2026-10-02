using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;
using Zonk.Core.Dice;
using Zonk.Core.Progress;

namespace Zonk.Progress
{
    [Serializable]
    public sealed class ChestSave
    {
        /// <summary>Побед до следующего сундука (накоплено).</summary>
        public int Wins;

        /// <summary>Сундуков ждёт открытия.</summary>
        public int Ready;

        public int Opened;

        /// <summary>Сундуков подряд без легендарки (счётчик невезения).</summary>
        public int SinceLegendary;
    }

    /// <summary>Что дал сундук: награда (монеты или вещь), её редкость, сработала ли гарантия легендарки.</summary>
    public readonly struct ChestOpening
    {
        public ChestOpening(GrantedReward reward, Rarity rarity, bool pity)
        {
            Reward = reward;
            Rarity = rarity;
            Pity = pity;
        }

        public GrantedReward Reward { get; }
        public Rarity Rarity { get; }
        public bool Pity { get; }
    }

    /// <summary>Сундуки за победы. Настройки — ChestConfig (GameConfig.Chest), розыгрыш — Zonk.Core.Progress.ChestRoller.</summary>
    public interface IChestService
    {
        bool IsEnabled { get; }
        int Ready { get; }
        int Wins { get; }
        int WinsPerChest { get; }
        int Opened { get; }

        /// <summary>Победа над соперником. true — набрался новый сундук.</summary>
        bool AddWin();

        /// <summary>Открыть сундук (если есть): награда выдаётся сразу.</summary>
        ChestOpening? Open();

        event Action Changed;
    }

    public sealed class ChestService : IChestService
    {
        private readonly ISaveStore _saves;
        private readonly ChestConfig _config;
        private readonly IInventory _inventory;
        private readonly IWallet _wallet;

        // Рабочие списки розыгрыша: сундук открывается редко, но списки всё равно переиспользуются.
        private readonly List<ChestDropRule> _rules = new List<ChestDropRule>();
        private readonly int[] _available = new int[3];
        private readonly List<CosmeticItemConfig> _candidates = new List<CosmeticItemConfig>();

        public ChestService(ISaveStore saves, GameConfig config, IInventory inventory, IWallet wallet)
        {
            _saves = saves;
            _config = config != null ? config.Chest : null;
            _inventory = inventory;
            _wallet = wallet;
        }

        public event Action Changed;

        private ChestSave Data => _saves.Get<ChestSave>(SaveKeys.Chest);

        public bool IsEnabled => _config != null;
        public int Ready => Data.Ready;
        public int Wins => Data.Wins;
        public int WinsPerChest => _config != null ? Math.Max(1, _config.WinsPerChest) : 1;
        public int Opened => Data.Opened;

        public bool AddWin()
        {
            if (_config == null)
                return false;

            var data = Data;
            data.Wins++;
            var earned = false;
            if (data.Wins >= WinsPerChest)
            {
                data.Wins = 0;
                if (data.Ready < Math.Max(1, _config.MaxStored))
                {
                    data.Ready++;
                    earned = true;
                }
            }

            _saves.RequestSave();
            Changed?.Invoke();
            return earned;
        }

        public ChestOpening? Open()
        {
            var data = Data;
            if (_config == null || data.Ready <= 0)
                return null;

            BuildRules();
            CountAvailable();
            var roll = ChestRoller.Roll(new SplitMixRandom(SplitMixRandom.NewSeed()), _rules, _available, data.SinceLegendary,
                _config.LegendaryPity);

            data.Ready--;
            data.Opened++;
            GrantedReward reward;
            var rarity = (Rarity)roll.Rarity;
            if (roll.IsItem)
            {
                var item = PickItem(rarity, roll.ItemIndex);
                _inventory.Grant(item);
                reward = new GrantedReward(item, null, 1);
            }
            else
            {
                if (_config.Coins != null && roll.Coins > 0)
                    _wallet.Add(_config.Coins, roll.Coins);
                reward = new GrantedReward(null, _config.Coins, roll.Coins);
                rarity = Rarity.Common;
            }

            data.SinceLegendary = roll.IsItem && rarity == Rarity.Legendary ? 0 : data.SinceLegendary + 1;
            _saves.RequestSave();
            Changed?.Invoke();
            return new ChestOpening(reward, rarity, roll.Pity);
        }

        private void BuildRules()
        {
            _rules.Clear();
            foreach (var drop in _config.Drops)
            {
                if (drop == null)
                    continue;
                _rules.Add(new ChestDropRule
                {
                    Item = drop.Item,
                    Rarity = (int)drop.Rarity,
                    Weight = drop.Weight,
                    MinCoins = drop.MinCoins,
                    MaxCoins = drop.MaxCoins,
                });
            }
        }

        private void CountAvailable()
        {
            Array.Clear(_available, 0, _available.Length);
            foreach (var item in _config.Items)
            {
                if (item != null && !_inventory.IsOwned(item))
                    _available[(int)item.Rarity]++;
            }
        }

        /// <summary>index-я не открытая вещь редкости rarity в порядке списка Items.</summary>
        private CosmeticItemConfig PickItem(Rarity rarity, int index)
        {
            _candidates.Clear();
            foreach (var item in _config.Items)
            {
                if (item != null && item.Rarity == rarity && !_inventory.IsOwned(item))
                    _candidates.Add(item);
            }

            return _candidates[Math.Min(index, _candidates.Count - 1)];
        }
    }
}
