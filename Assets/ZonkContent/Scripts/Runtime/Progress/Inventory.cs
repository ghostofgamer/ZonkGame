using System;
using Base.Services.Monetization;
using Base.Services.Saves;
using Zonk.Configs;

namespace Zonk.Progress
{
    /// <summary>Что открыто у игрока: косметика, особые кости, наборы.</summary>
    public interface IInventory
    {
        /// <summary>Бесплатный предмет, выданный или купленный за деньги (право в IEntitlements).</summary>
        bool IsOwned(ContentConfig item);

        void Grant(ContentConfig item);

        /// <summary>Сколько реклам уже просмотрено ради предмета.</summary>
        int GetAdProgress(ContentConfig item);

        void AddAdProgress(ContentConfig item);

        event Action<ContentConfig> Granted;
    }

    public static class Pricing
    {
        /// <summary>Цена предмета любого типа. null: у типа цены нет, он открыт всегда.</summary>
        public static Price PriceOf(ContentConfig item)
        {
            switch (item)
            {
                case CosmeticItemConfig cosmetic:
                    return cosmetic.Price;
                case DieConfig die:
                    return die.Price;
                case ThemeSetConfig theme:
                    return theme.Price;
                default:
                    return null;
            }
        }
    }

    public sealed class Inventory : IInventory
    {
        private readonly ISaveStore _saves;
        private readonly IEntitlements _entitlements;

        public Inventory(ISaveStore saves, IEntitlements entitlements)
        {
            _saves = saves;
            _entitlements = entitlements;
        }

        public event Action<ContentConfig> Granted;

        private InventorySave Data => _saves.Get<InventorySave>(SaveKeys.Inventory);

        public bool IsOwned(ContentConfig item)
        {
            if (item == null)
                return false;

            var price = Pricing.PriceOf(item);
            if (price == null || price.IsFree)
                return true;

            if (Data.Owned.Contains(item.Id))
                return true;

            // Купленное за деньги хранится правом площадки и восстанавливается при запуске.
            foreach (var option in price.Options)
            {
                if (option is PurchasePriceOption purchase && !string.IsNullOrEmpty(purchase.ProductId) &&
                    _entitlements.Has(purchase.ProductId))
                {
                    return true;
                }
            }

            return false;
        }

        public void Grant(ContentConfig item)
        {
            if (item == null)
                return;

            if (item is ThemeSetConfig theme)
            {
                foreach (var themeItem in theme.Items)
                    Grant(themeItem);
            }

            var data = Data;
            if (data.Owned.Contains(item.Id))
                return;

            data.Owned.Add(item.Id);
            _saves.RequestSave();
            Granted?.Invoke(item);
        }

        public int GetAdProgress(ContentConfig item)
        {
            foreach (var entry in Data.AdProgress)
            {
                if (entry.Id == item.Id)
                    return entry.Watched;
            }

            return 0;
        }

        public void AddAdProgress(ContentConfig item)
        {
            var data = Data;
            foreach (var entry in data.AdProgress)
            {
                if (entry.Id == item.Id)
                {
                    entry.Watched++;
                    _saves.RequestSave();
                    return;
                }
            }

            data.AdProgress.Add(new AdProgressEntry { Id = item.Id, Watched = 1 });
            _saves.RequestSave();
        }
    }
}
