using System;
using Base.Services.Saves;
using Zonk.Configs;

namespace Zonk.Progress
{
    /// <summary>Кошелёк: все валюты по ID. Энергия восстанавливается по реальному времени до потолка.</summary>
    public interface IWallet
    {
        int Get(CurrencyConfig currency);
        void Add(CurrencyConfig currency, int amount);
        bool TrySpend(CurrencyConfig currency, int amount);

        /// <summary>Сколько ждать до следующего восстановления. null: восстановления нет или уже потолок.</summary>
        TimeSpan? TimeToNextRegen(CurrencyConfig currency);

        event Action<CurrencyConfig> Changed;
    }

    public sealed class Wallet : IWallet
    {
        private readonly ISaveStore _saves;

        public Wallet(ISaveStore saves)
        {
            _saves = saves;
        }

        public event Action<CurrencyConfig> Changed;

        private WalletSave Data => _saves.Get<WalletSave>(SaveKeys.Wallet);

        public int Get(CurrencyConfig currency)
        {
            if (currency == null)
                return 0;

            var entry = Entry(currency);
            if (ApplyRegen(currency, entry))
                _saves.RequestSave();
            return entry.Amount;
        }

        public void Add(CurrencyConfig currency, int amount)
        {
            if (currency == null || amount == 0)
                return;

            var entry = Entry(currency);
            ApplyRegen(currency, entry);
            entry.Amount = Math.Max(0, entry.Amount + amount);
            _saves.RequestSave();
            Changed?.Invoke(currency);
        }

        public bool TrySpend(CurrencyConfig currency, int amount)
        {
            if (currency == null)
                return false;
            if (amount <= 0)
                return true;

            var entry = Entry(currency);
            ApplyRegen(currency, entry);
            if (entry.Amount < amount)
                return false;

            // Трата с потолка запускает отсчёт восстановления с этого момента.
            if (currency.HasRegen && entry.Amount >= currency.RegenCap)
                entry.RegenUnix = Now();

            entry.Amount -= amount;
            _saves.RequestSave();
            Changed?.Invoke(currency);
            return true;
        }

        public TimeSpan? TimeToNextRegen(CurrencyConfig currency)
        {
            if (currency == null || !currency.HasRegen)
                return null;

            var entry = Entry(currency);
            ApplyRegen(currency, entry);
            if (entry.Amount >= currency.RegenCap)
                return null;

            var next = entry.RegenUnix + currency.RegenIntervalSeconds - Now();
            return TimeSpan.FromSeconds(Math.Max(0, next));
        }

        private CurrencyEntry Entry(CurrencyConfig currency)
        {
            var data = Data;
            foreach (var entry in data.Currencies)
            {
                if (entry.Id == currency.Id)
                    return entry;
            }

            var created = new CurrencyEntry { Id = currency.Id, Amount = currency.StartAmount, RegenUnix = Now() };
            data.Currencies.Add(created);
            _saves.RequestSave();
            return created;
        }

        /// <summary>Начисляет восстановление за прошедшее время. true, если что-то изменилось.</summary>
        private bool ApplyRegen(CurrencyConfig currency, CurrencyEntry entry)
        {
            if (!currency.HasRegen)
                return false;

            var now = Now();
            if (entry.Amount >= currency.RegenCap)
            {
                entry.RegenUnix = now;
                return false;
            }

            // Часы устройства могли уйти назад: отсчёт начинается заново, а не копит отрицательное.
            if (entry.RegenUnix > now)
                entry.RegenUnix = now;

            var steps = (now - entry.RegenUnix) / currency.RegenIntervalSeconds;
            if (steps <= 0)
                return false;

            entry.Amount = (int)Math.Min(currency.RegenCap, entry.Amount + steps * currency.RegenAmount);
            entry.RegenUnix += steps * currency.RegenIntervalSeconds;
            Changed?.Invoke(currency);
            return true;
        }

        private static long Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
