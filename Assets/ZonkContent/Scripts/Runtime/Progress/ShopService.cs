using System.Collections.Generic;
using System.Threading;
using Base.Services.Monetization;
using Base.Services.Saves;
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
        private readonly ITalents _talents;
        private readonly IWallet _wallet;
        private readonly IInventory _inventory;
        private readonly IRewardService _rewards;
        private readonly IPurchaseFlow _purchases;
        private readonly IQuestService _quests;
        private readonly RewardGranter _granter;
        private readonly ContentDatabase _content;
        private readonly ISaveStore _saves;
        private readonly Dictionary<string, string> _prices = new Dictionary<string, string>();

        /// <summary>Тег события заданий: игрок получил предмет в магазине (CustomEventGoal).</summary>
        public const string AcquiredTag = "shop_acquired";

        public ShopService(IWallet wallet, IInventory inventory, IRewardService rewards, IPurchaseFlow purchases, IQuestService quests,
            RewardGranter granter, ContentDatabase content, ISaveStore saves, ITalents talents = null)
        {
            _talents = talents;
            _wallet = wallet;
            _inventory = inventory;
            _rewards = rewards;
            _purchases = purchases;
            _quests = quests;
            _granter = granter;
            _content = content;
            _saves = saves;

            // Оплаченное (в том числе восстановленное при запуске) сразу выдаётся в игре.
            purchases.Delivered += _ => SyncPurchases();
        }

        /// <summary>На площадке есть покупки за деньги.</summary>
        public bool IsPurchaseAvailable => _purchases.IsAvailable;

        /// <summary>Цена за монеты со скидкой таланта (ShopCoinDiscountPercent, не больше 50%).</summary>
        public int CoinPrice(CurrencyPriceOption option)
        {
            if (option == null)
                return 0;
            var discount = _talents != null ? Mathf.Clamp(_talents.Value(TalentEffect.ShopCoinDiscountPercent), 0f, 50f) : 0f;
            return Mathf.Max(0, Mathf.RoundToInt(option.Amount * (1f - discount / 100f)));
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
            var owned = _inventory.IsOwned(item);
            var result = await AcquireCoreAsync(item, option, ct);
            if (result == AcquireResult.Acquired && !owned)
                _quests.Report(QuestEvent.CustomEvent(AcquiredTag));
            if (result == AcquireResult.Acquired)
                SyncPurchases();
            return result;
        }

        private async UniTask<AcquireResult> AcquireCoreAsync(ContentConfig item, PriceOption option, CancellationToken ct)
        {
            if (_inventory.IsOwned(item))
                return AcquireResult.Acquired;

            switch (option)
            {
                case CurrencyPriceOption currency:
                    if (!_wallet.TrySpend(currency.Currency, CoinPrice(currency)))
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

        /// <summary>Купить пакет монет за деньги. Монеты зачисляются в SyncPurchases.</summary>
        public async UniTask<AcquireResult> BuyCoinPackAsync(CoinPackConfig pack, CancellationToken ct)
        {
            if (pack == null || string.IsNullOrEmpty(pack.ProductId) || !_purchases.IsAvailable)
                return AcquireResult.Unavailable;

            var outcome = await _purchases.BuyAsync(pack.ProductId, ct);
            SyncPurchases();
            switch (outcome)
            {
                case PurchaseOutcome.Success: return AcquireResult.Acquired;
                case PurchaseOutcome.Failed: return AcquireResult.Cancelled;
                default: return AcquireResult.Unavailable;
            }
        }

        /// <summary>
        /// Выдать оплаченное, но ещё не выданное: монеты расходуемых пакетов и разовые награды наборов.
        /// Вызывается после покупки, при восстановлении покупок и при каждом входе в меню. Повторный вызов ничего не выдаёт.
        /// </summary>
        public void SyncPurchases()
        {
            if (!_saves.IsLoaded)
                return;

            foreach (var pack in _content.All<CoinPackConfig>())
            {
                var count = string.IsNullOrEmpty(pack.ProductId) ? 0 : _purchases.ClaimConsumable(pack.ProductId);
                if (count > 0 && pack.Currency != null)
                    _wallet.Add(pack.Currency, pack.Amount * count);
            }

            var data = _saves.Get<InventorySave>(SaveKeys.Inventory);
            foreach (var theme in _content.All<ThemeSetConfig>())
            {
                if (theme.Rewards.Count == 0 || data.RewardedThemes.Contains(theme.Id) || !_inventory.IsOwned(theme))
                    continue;

                data.RewardedThemes.Add(theme.Id);
                _granter.Grant(theme.Rewards);
                _saves.RequestSave();
            }
        }

        /// <summary>Цены товаров из каталога площадки («99 RUB»). Загружаются один раз, до этого PriceText пустой.</summary>
        public async UniTask LoadPricesAsync(CancellationToken ct)
        {
            if (_prices.Count > 0 || !_purchases.IsAvailable)
                return;

            var products = await _purchases.GetProductsAsync(ct);
            foreach (var product in products)
            {
                if (product != null && !string.IsNullOrEmpty(product.Id) && !string.IsNullOrEmpty(product.PriceFormatted))
                    _prices[product.Id] = product.PriceFormatted;
            }
        }

        public string PriceText(string productId)
        {
            return productId != null && _prices.TryGetValue(productId, out var price) ? price : null;
        }
    }
}
