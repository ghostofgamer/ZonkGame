using System.Collections.Generic;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Progress
{
    /// <summary>Выданная награда для показа на экране итогов.</summary>
    public readonly struct GrantedReward
    {
        public GrantedReward(ContentConfig item, CurrencyConfig currency, int amount)
        {
            Item = item;
            Currency = currency;
            Amount = amount;
        }

        public ContentConfig Item { get; }
        public CurrencyConfig Currency { get; }
        public int Amount { get; }
    }

    /// <summary>Выдаёт награды из конфигов. Новый тип Reward добавляется сюда одной веткой.</summary>
    public sealed class RewardGranter
    {
        private readonly IWallet _wallet;
        private readonly IInventory _inventory;

        public RewardGranter(IWallet wallet, IInventory inventory)
        {
            _wallet = wallet;
            _inventory = inventory;
        }

        public List<GrantedReward> Grant(IEnumerable<Reward> rewards, int multiplier = 1)
        {
            var granted = new List<GrantedReward>();
            if (rewards == null)
                return granted;

            foreach (var reward in rewards)
            {
                switch (reward)
                {
                    case CurrencyReward currency when currency.Currency != null:
                        var amount = currency.Amount * Mathf.Max(1, multiplier);
                        _wallet.Add(currency.Currency, amount);
                        granted.Add(new GrantedReward(null, currency.Currency, amount));
                        break;
                    case ContentReward content when content.Item != null:
                        if (!_inventory.IsOwned(content.Item))
                        {
                            _inventory.Grant(content.Item);
                            granted.Add(new GrantedReward(content.Item, null, 1));
                        }

                        break;
                    case null:
                        break;
                    default:
                        Debug.LogWarning($"[Rewards] Unsupported reward type {reward.GetType().Name}");
                        break;
                }
            }

            return granted;
        }
    }
}
