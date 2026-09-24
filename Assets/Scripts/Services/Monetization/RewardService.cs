using System.Threading;
using Cysharp.Threading.Tasks;
using Base.Platform;
using UnityEngine;

namespace Base.Services.Monetization
{
    public sealed class RewardService : IRewardService
    {
        private readonly IAdsService _ads;
        private readonly IEntitlements _entitlements;
        private readonly RewardRules _rules;
        private bool _showing;

        public RewardService(IAdsService ads, IEntitlements entitlements, MonetizationConfig config)
        {
            _ads = ads;
            _entitlements = entitlements;
            _rules = config.Rewards;
        }

        public bool CanOffer => !_showing && (IsFree || _ads.IsRewardedAvailable || _rules.FreeWhenAdsUnavailable);

        private bool IsFree => _rules.FreeWithNoAds && _entitlements.Has(EntitlementIds.NoAds);

        public async UniTask<RewardOutcome> RequestAsync(string placement, CancellationToken cancellationToken = default)
        {
            if (IsFree)
                return Log(placement, RewardOutcome.Free);

            // Второй показ поверх первого площадки не поддерживают.
            if (_showing)
                return Log(placement, RewardOutcome.Unavailable);

            _showing = true;
            RewardedAdResult result;
            try
            {
                result = await _ads.ShowRewardedAsync(placement, cancellationToken);
            }
            finally
            {
                _showing = false;
            }

            switch (result)
            {
                case RewardedAdResult.Rewarded:
                    return Log(placement, RewardOutcome.Rewarded);
                case RewardedAdResult.Closed:
                    return Log(placement, RewardOutcome.Closed);
                default:
                    return Log(placement, _rules.FreeWhenAdsUnavailable ? RewardOutcome.Free : RewardOutcome.Unavailable);
            }
        }

        private static RewardOutcome Log(string placement, RewardOutcome outcome)
        {
            Debug.Log($"[Ads] Reward '{placement}' -> {outcome}");
            return outcome;
        }
    }
}
