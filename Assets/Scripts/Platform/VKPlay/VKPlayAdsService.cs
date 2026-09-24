using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKPlay
{
    /// <summary>
    /// Реклама VK Play. TODO: VKWebAppShowNativeAds с ad_format "interstitial" и "reward",
    /// VKWebAppCheckNativeAds для проверки доступности.
    /// </summary>
    public sealed class VKPlayAdsService : IAdsService
    {
        public bool IsInterstitialAvailable => false;
        public bool IsRewardedAvailable => false;

        public event Action AdOpened;
        public event Action AdClosed;

        public UniTask<bool> ShowInterstitialAsync(CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[VKPlay] ShowInterstitial: SDK not integrated yet");
            return UniTask.FromResult(false);
        }

        public UniTask<RewardedAdResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default)
        {
            Debug.LogWarning($"[VKPlay] ShowRewarded '{placement}': SDK not integrated yet");
            return UniTask.FromResult(RewardedAdResult.NotAvailable);
        }

        private void RaiseOpened() => AdOpened?.Invoke();
        private void RaiseClosed() => AdClosed?.Invoke();
    }
}
