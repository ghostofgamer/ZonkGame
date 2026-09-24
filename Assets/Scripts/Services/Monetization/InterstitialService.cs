using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Base.Platform;
using UnityEngine;
using Zenject;

namespace Base.Services.Monetization
{
    /// <summary>
    /// Считает время от запуска и от последней закрытой рекламы (любой, в том числе rewarded),
    /// чтобы игрок не получил interstitial сразу после досмотренного ролика за награду.
    /// Правила действуют в пределах сессии: после перезапуска игры снова работает FirstShowDelay.
    /// </summary>
    public sealed class InterstitialService : IInterstitialService, IInitializable, IDisposable
    {
        private readonly IAdsService _ads;
        private readonly IEntitlements _entitlements;
        private readonly InterstitialRules _rules;

        private float _lastAdClosedAt = float.NegativeInfinity;
        private int _triggersSinceShow;
        private bool _showing;

        public InterstitialService(IAdsService ads, IEntitlements entitlements, MonetizationConfig config)
        {
            _ads = ads;
            _entitlements = entitlements;
            _rules = config.Interstitial;
        }

        public void Initialize()
        {
            _ads.AdClosed += OnAdClosed;
        }

        public void Dispose()
        {
            _ads.AdClosed -= OnAdClosed;
        }

        public async UniTask<bool> TryShowAsync(string trigger, CancellationToken cancellationToken = default)
        {
            var skipReason = GetSkipReason();
            if (skipReason != null)
            {
                Debug.Log($"[Ads] Interstitial '{trigger}' skipped: {skipReason}");
                return false;
            }

            _showing = true;
            try
            {
                var shown = await _ads.ShowInterstitialAsync(cancellationToken);
                if (shown)
                {
                    _triggersSinceShow = 0;
                    _lastAdClosedAt = Time.realtimeSinceStartup;
                }

                Debug.Log($"[Ads] Interstitial '{trigger}' shown={shown}");
                return shown;
            }
            finally
            {
                _showing = false;
            }
        }

        /// <summary>Причина не показывать рекламу или null, если показывать можно. Каждый вызов считается поводом.</summary>
        private string GetSkipReason()
        {
            if (_entitlements.Has(EntitlementIds.NoAds))
                return "no_ads purchased";

            if (_showing)
                return "already showing";

            _triggersSinceShow++;

            var now = Time.realtimeSinceStartup;
            if (now < _rules.FirstShowDelay)
                return $"first {_rules.FirstShowDelay:0}s after launch ({now:0}s passed)";

            var sinceLastAd = now - _lastAdClosedAt;
            if (sinceLastAd < _rules.MinInterval)
                return $"{sinceLastAd:0}s since last ad, need {_rules.MinInterval:0}s";

            if (_triggersSinceShow < Math.Max(1, _rules.EveryNthTrigger))
                return $"trigger {_triggersSinceShow} of {_rules.EveryNthTrigger}";

            return null;
        }

        private void OnAdClosed()
        {
            _lastAdClosedAt = Time.realtimeSinceStartup;
        }
    }
}
