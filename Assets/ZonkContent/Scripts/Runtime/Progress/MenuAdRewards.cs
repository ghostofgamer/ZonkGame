using System.Collections.Generic;
using System.Threading;
using Base.Services.Monetization;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using Zonk.Configs;
using Zonk.Core.Progress;

namespace Zonk.Progress
{
    /// <summary>
    /// Награды за рекламу из главного меню (GameConfig.MenuAdOffers): монеты, энергия и что угодно ещё по строке конфига.
    /// У каждой кнопки свой лимит в день (день — по местной полуночи, как у заданий). Реклама только через IRewardService.
    /// </summary>
    public sealed class MenuAdRewards
    {
        private readonly ISaveStore _saves;
        private readonly GameConfig _config;
        private readonly IWallet _wallet;
        private readonly IRewardService _rewards;
        private readonly IGameClock _clock;

        public MenuAdRewards(ISaveStore saves, GameConfig config, IWallet wallet, IRewardService rewards, IGameClock clock)
        {
            _saves = saves;
            _config = config;
            _wallet = wallet;
            _rewards = rewards;
            _clock = clock;
        }

        public IReadOnlyList<MenuAdOffer> Offers => _config.MenuAdOffers;

        private AdBonusSave Data
        {
            get
            {
                var data = _saves.Get<AdBonusSave>(SaveKeys.AdBonus);
                var today = QuestCalendar.DayIndex(_clock.LocalNow);
                if (data.Day != today)
                {
                    data.Day = today;
                    data.Offers.Clear();
                }

                return data;
            }
        }

        /// <summary>Сколько просмотров осталось сегодня; без лимита (PerDay ≤ 0) — int.MaxValue.</summary>
        public int Left(MenuAdOffer offer)
        {
            if (offer == null)
                return 0;
            if (offer.PerDay <= 0)
                return int.MaxValue;

            foreach (var entry in Data.Offers)
            {
                if (entry.Id == offer.Id)
                    return System.Math.Max(0, offer.PerDay - entry.Count);
            }

            return offer.PerDay;
        }

        /// <summary>Показывать кнопку вообще: строка настроена и валюта не на потолке (если так задано в HideWhenFull).</summary>
        public bool IsShown(MenuAdOffer offer)
        {
            if (offer == null || offer.Currency == null || offer.Amount <= 0)
                return false;

            return !(offer.HideWhenFull && offer.Currency.HasRegen && _wallet.Get(offer.Currency) >= offer.Currency.RegenCap);
        }

        /// <summary>Реклама загружена и её можно показать (на VK — предзагрузка, меняется со временем).</summary>
        public bool IsAdReady => _rewards.CanOffer;

        /// <summary>Можно нажать: кнопка видна, просмотры на сегодня остались, реклама готова.</summary>
        public bool CanOffer(MenuAdOffer offer)
        {
            return IsShown(offer) && Left(offer) > 0 && IsAdReady;
        }

        /// <summary>Показать рекламу и выдать награду. Возвращает выданное количество (0 — не вышло).</summary>
        public async UniTask<int> ClaimAsync(MenuAdOffer offer, CancellationToken ct)
        {
            if (!CanOffer(offer))
                return 0;

            var outcome = await _rewards.RequestAsync("menu_" + offer.Id, ct);
            if (!outcome.IsGranted() || Left(offer) <= 0)
                return 0;

            var data = Data;
            var entry = data.Offers.Find(e => e.Id == offer.Id);
            if (entry == null)
                data.Offers.Add(entry = new AdBonusEntry { Id = offer.Id });

            entry.Count++;
            _wallet.Add(offer.Currency, offer.Amount);
            _saves.RequestSave();
            return offer.Amount;
        }
    }
}
