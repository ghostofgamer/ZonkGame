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

        /// <summary>Потолок восстановления валюты с учётом талантов (энергия).</summary>
        int CapOf(CurrencyConfig currency);

        /// <summary>Сколько ждать до следующего восстановления. null: восстановления нет или уже потолок.</summary>
        TimeSpan? TimeToNextRegen(CurrencyConfig currency);

        event Action<CurrencyConfig> Changed;
    }

    public sealed class Wallet : IWallet
    {
        private readonly ISaveStore _saves;
        private readonly ITalents _talents;
        private readonly CurrencyConfig _energy;

        public Wallet(ISaveStore saves, ITalents talents = null, GameConfig config = null)
        {
            _saves = saves;
            _talents = talents;
            _energy = config != null ? config.Energy : null;
        }

        /// <summary>Потолок: у энергии — плюс таланты (EnergyMax).</summary>
        public int CapOf(CurrencyConfig currency)
        {
            if (currency == null)
                return 0;
            var extra = _talents != null && currency == _energy ? (int)_talents.Value(TalentEffect.EnergyMax) : 0;
            return currency.RegenCap + extra;
        }

        /// <summary>Интервал восстановления: у энергии быстрее на EnergyRegenPercent (не быстрее чем вдвое).</summary>
        private long IntervalOf(CurrencyConfig currency)
        {
            var percent = _talents != null && currency == _energy ? _talents.Value(TalentEffect.EnergyRegenPercent) : 0f;
            var factor = 1.0 - Math.Min(50.0, Math.Max(0.0, percent)) / 100.0;
            return Math.Max(1L, (long)Math.Round(currency.RegenIntervalSeconds * factor));
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
            if (currency.HasRegen && entry.Amount >= CapOf(currency))
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
            if (entry.Amount >= CapOf(currency))
                return null;

            var next = entry.RegenUnix + IntervalOf(currency) - Now();
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
            if (entry.Amount >= CapOf(currency))
            {
                entry.RegenUnix = now;
                return false;
            }

            // Часы устройства могли уйти назад: отсчёт начинается заново, а не копит отрицательное.
            if (entry.RegenUnix > now)
                entry.RegenUnix = now;

            var steps = (now - entry.RegenUnix) / IntervalOf(currency);
            if (steps <= 0)
                return false;

            entry.Amount = (int)Math.Min(CapOf(currency), entry.Amount + steps * currency.RegenAmount);
            entry.RegenUnix += steps * IntervalOf(currency);
            Changed?.Invoke(currency);
            return true;
        }

        private static long Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
