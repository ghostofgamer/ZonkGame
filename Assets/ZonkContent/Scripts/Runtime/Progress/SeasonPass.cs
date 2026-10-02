using System;
using System.Collections.Generic;
using System.Threading;
using Base.Services.Monetization;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using Zonk.Configs;

namespace Zonk.Progress
{
    [Serializable]
    public sealed class SeasonSave
    {
        /// <summary>Сезон, к которому относятся очки и забранные награды: новый сезон начинается с нуля.</summary>
        public string Id;

        public int Points;
        public List<int> Free = new List<int>();
        public List<int> Premium = new List<int>();
    }

    /// <summary>Как сейчас открывается платная награда ступени.</summary>
    public enum PremiumAccess
    {
        /// <summary>Платной дорожки нет или ступень ещё не пройдена.</summary>
        None,

        /// <summary>Куплена платная дорожка.</summary>
        Pass,

        /// <summary>Покупки на площадке есть, дорожка не куплена.</summary>
        NeedsPass,

        /// <summary>Покупок нет: за просмотр рекламы.</summary>
        Ad,

        /// <summary>Покупок нет: игрок ушёл достаточно далеко — даром.</summary>
        Free,
    }

    /// <summary>
    /// Сезонный путь: текущий сезон по дате, очки (опыт за партии), две дорожки наград. Платная — правом площадки
    /// (товар PassProductId, выдача через IPurchaseFlow), на площадке без покупок — за рекламу или прогресс.
    /// Купивший в середине сезона забирает платные награды всех пройденных ступеней.
    /// </summary>
    public interface ISeasonPass
    {
        /// <summary>Идущий сезон или null.</summary>
        SeasonConfig Current { get; }

        int Points { get; }

        /// <summary>Пройдено ступеней (0..Steps.Count).</summary>
        int Step { get; }

        /// <summary>Очков набрано на текущей ступени и сколько нужно на ступень.</summary>
        void StepProgress(out int into, out int perStep);

        TimeSpan Left { get; }
        bool HasPass { get; }
        bool CanBuyPass { get; }

        bool IsClaimedFree(int step);
        bool IsClaimedPremium(int step);
        bool CanClaimFree(int step);
        PremiumAccess PremiumAccessFor(int step);

        void AddPoints(int points);
        List<GrantedReward> ClaimFree(int step);

        /// <summary>Платная награда: по праву, за рекламу или даром (PremiumAccessFor). null — не получилось.</summary>
        UniTask<List<GrantedReward>> ClaimPremiumAsync(int step, CancellationToken ct);

        UniTask<bool> BuyPassAsync(CancellationToken ct);

        event Action Changed;
    }

    public sealed class SeasonPass : ISeasonPass
    {
        public const string AdPlacement = "season_premium";

        private readonly ISaveStore _saves;
        private readonly ContentDatabase _content;
        private readonly RewardGranter _granter;
        private readonly IGameClock _clock;
        private readonly IPurchaseFlow _purchases;
        private readonly IEntitlements _entitlements;
        private readonly IRewardService _rewards;

        public SeasonPass(ISaveStore saves, ContentDatabase content, RewardGranter granter, IGameClock clock, IPurchaseFlow purchases,
            IEntitlements entitlements, IRewardService rewards)
        {
            _saves = saves;
            _content = content;
            _granter = granter;
            _clock = clock;
            _purchases = purchases;
            _entitlements = entitlements;
            _rewards = rewards;
            _entitlements.Changed += () => Changed?.Invoke();
        }

        public event Action Changed;

        private DateTime UtcNow => _clock.LocalNow.ToUniversalTime();

        public SeasonConfig Current
        {
            get
            {
                var now = UtcNow;
                foreach (var season in _content.All<SeasonConfig>())
                {
                    if (season.IsActive(now) && season.Steps.Count > 0)
                        return season;
                }

                return null;
            }
        }

        /// <summary>Сохранение текущего сезона: другой сезон в сохранении — начать заново.</summary>
        private SeasonSave Data(SeasonConfig season)
        {
            var data = _saves.Get<SeasonSave>(SaveKeys.Season);
            if (season != null && data.Id != season.Id)
            {
                data.Id = season.Id;
                data.Points = 0;
                data.Free.Clear();
                data.Premium.Clear();
                _saves.RequestSave();
            }

            return data;
        }

        public int Points
        {
            get
            {
                var season = Current;
                return season != null ? Data(season).Points : 0;
            }
        }

        public int Step
        {
            get
            {
                var season = Current;
                return season != null ? Math.Min(season.Steps.Count, Data(season).Points / Math.Max(1, season.PointsPerStep)) : 0;
            }
        }

        public void StepProgress(out int into, out int perStep)
        {
            var season = Current;
            perStep = season != null ? Math.Max(1, season.PointsPerStep) : 1;
            into = season != null && Step < season.Steps.Count ? Data(season).Points % perStep : perStep;
        }

        public TimeSpan Left
        {
            get
            {
                var season = Current;
                return season != null ? season.EndUtc - UtcNow : TimeSpan.Zero;
            }
        }

        public bool HasPass
        {
            get
            {
                var season = Current;
                return season != null && !string.IsNullOrEmpty(season.PassProductId) && _entitlements.Has(season.PassProductId);
            }
        }

        public bool CanBuyPass
        {
            get
            {
                var season = Current;
                return season != null && !string.IsNullOrEmpty(season.PassProductId) && _purchases.IsAvailable && !HasPass;
            }
        }

        public bool IsClaimedFree(int step)
        {
            var season = Current;
            return season != null && Data(season).Free.Contains(step);
        }

        public bool IsClaimedPremium(int step)
        {
            var season = Current;
            return season != null && Data(season).Premium.Contains(step);
        }

        public bool CanClaimFree(int step)
        {
            var season = Current;
            return season != null && step >= 0 && step < Step && !IsClaimedFree(step) && HasRewards(season.Steps[step].Free);
        }

        public PremiumAccess PremiumAccessFor(int step)
        {
            var season = Current;
            if (season == null || step < 0 || step >= Step || IsClaimedPremium(step) || !HasRewards(season.Steps[step].Premium))
                return PremiumAccess.None;

            if (HasPass)
                return PremiumAccess.Pass;

            // Покупки на площадке есть: платная дорожка продаётся, обходных путей нет.
            if (_purchases.IsAvailable && !string.IsNullOrEmpty(season.PassProductId))
                return PremiumAccess.NeedsPass;

            if (season.PremiumFreeAfterSteps > 0 && step + season.PremiumFreeAfterSteps <= Step)
                return PremiumAccess.Free;

            return season.PremiumForAd && _rewards.CanOffer ? PremiumAccess.Ad : PremiumAccess.None;
        }

        public void AddPoints(int points)
        {
            var season = Current;
            if (season == null || points <= 0)
                return;

            var data = Data(season);
            data.Points = (int)Math.Min(int.MaxValue, (long)data.Points + points);
            _saves.RequestSave();
            Changed?.Invoke();
        }

        public List<GrantedReward> ClaimFree(int step)
        {
            if (!CanClaimFree(step))
                return null;

            var season = Current;
            Data(season).Free.Add(step);
            var granted = _granter.Grant(season.Steps[step].Free);
            _saves.RequestSave();
            Changed?.Invoke();
            return granted;
        }

        public async UniTask<List<GrantedReward>> ClaimPremiumAsync(int step, CancellationToken ct)
        {
            var access = PremiumAccessFor(step);
            if (access == PremiumAccess.None || access == PremiumAccess.NeedsPass)
                return null;

            if (access == PremiumAccess.Ad)
            {
                var outcome = await _rewards.RequestAsync(AdPlacement, ct);
                if (!outcome.IsGranted() || IsClaimedPremium(step))
                    return null;
            }

            var season = Current;
            if (season == null)
                return null;

            Data(season).Premium.Add(step);
            var granted = _granter.Grant(season.Steps[step].Premium);
            await _saves.SaveNowAsync(ct);
            Changed?.Invoke();
            return granted;
        }

        public async UniTask<bool> BuyPassAsync(CancellationToken ct)
        {
            var season = Current;
            if (season == null || !CanBuyPass)
                return false;

            var outcome = await _purchases.BuyAsync(season.PassProductId, ct);
            Changed?.Invoke();
            return outcome == PurchaseOutcome.Success && HasPass;
        }

        private static bool HasRewards(List<Reward> rewards)
        {
            return rewards != null && rewards.Count > 0;
        }
    }
}
