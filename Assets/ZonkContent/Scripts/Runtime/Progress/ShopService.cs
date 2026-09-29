using System.Threading;
using Base.Services.Monetization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Progress
{
    public enum AcquireResult
    {
        Acquired,
        /// <summary>Реклама засчитана, но нужно ещё.</summary>
        Progress,
        NotEnoughCurrency,
        Cancelled,
        Unavailable,
    }

    /// <summary>
    /// Получение предметов по любому варианту цены. Реклама только через IRewardService,
    /// покупки только через IPurchaseFlow: так соблюдаются частота рекламы, no_ads и защита покупок.
    /// </summary>
    public sealed class ShopService
    {
        private readonly IWallet _wallet;
        private readonly IInventory _inventory;
        private readonly IRewardService _rewards;
        private readonly IPurchaseFlow _purchases;

        public ShopService(IWallet wallet, IInventory inventory, IRewardService rewards, IPurchaseFlow purchases)
        {
            _wallet = wallet;
            _inventory = inventory;
            _rewards = rewards;
            _purchases = purchases;
        }

        /// <summary>Можно ли показать этот вариант цены на текущей площадке.</summary>
        public bool IsOptionAvailable(PriceOption option)
        {
            switch (option)
            {
                case CurrencyPriceOption currency:
                    return currency.Currency != null;
                case RewardedAdPriceOption _:
                    return _rewards.CanOffer;
                case PurchasePriceOption purchase:
                    return _purchases.IsAvailable && !string.IsNullOrEmpty(purchase.ProductId);
                default:
                    return false;
            }
        }

        public async UniTask<AcquireResult> AcquireAsync(ContentConfig item, PriceOption option, CancellationToken ct)
        {
            if (_inventory.IsOwned(item))
                return AcquireResult.Acquired;

            switch (option)
            {
                case CurrencyPriceOption currency:
                    if (!_wallet.TrySpend(currency.Currency, currency.Amount))
                        return AcquireResult.NotEnoughCurrency;
                    _inventory.Grant(item);
                    return AcquireResult.Acquired;

                case RewardedAdPriceOption ads:
                {
                    var outcome = await _rewards.RequestAsync("unlock_" + item.Id, ct);
                    if (!outcome.IsGranted())
                        return outcome == RewardOutcome.Unavailable ? AcquireResult.Unavailable : AcquireResult.Cancelled;

                    _inventory.AddAdProgress(item);
                    if (_inventory.GetAdProgress(item) < Mathf.Max(1, ads.AdsRequired))
                        return AcquireResult.Progress;

                    _inventory.Grant(item);
                    return AcquireResult.Acquired;
                }

                case PurchasePriceOption purchase:
                {
                    var outcome = await _purchases.BuyAsync(purchase.ProductId, ct);
                    if (outcome != PurchaseOutcome.Success)
                        return outcome == PurchaseOutcome.Failed ? AcquireResult.Cancelled : AcquireResult.Unavailable;

                    // Постоянный товар стал правом, Inventory видит его через IEntitlements. Выдаём и явно,
                    // чтобы предметы набора открылись по отдельности.
                    _inventory.Grant(item);
                    return AcquireResult.Acquired;
                }

                default:
                    return AcquireResult.Unavailable;
            }
        }
    }
}
