using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Yandex
{
    /// <summary>
    /// Реклама Яндекс Игр: showFullscreenAdv и showRewardedVideo.
    /// SDK сам следит за минимальным интервалом между показами fullscreen-рекламы:
    /// при слишком частом вызове реклама просто не покажется, и метод вернёт false.
    /// </summary>
    public sealed class YandexAdsService : IAdsService
    {
        public YandexAdsService()
        {
            YandexBridge.AdOpened += () => AdOpened?.Invoke();
            YandexBridge.AdClosed += () => AdClosed?.Invoke();
        }

        /// <summary>SDK не сообщает о доступности заранее, поэтому ориентируемся на инициализацию.</summary>
        public bool IsInterstitialAvailable => YandexBridge.IsSupported && YandexSession.IsInitialized;

        public bool IsRewardedAvailable => YandexBridge.IsSupported && YandexSession.IsInitialized;

        public event Action AdOpened;
        public event Action AdClosed;

        public async UniTask<bool> ShowInterstitialAsync(CancellationToken cancellationToken = default)
        {
            if (!IsInterstitialAvailable)
                return false;

            try
            {
                var result = await YandexBridge.ShowInterstitialAsync(cancellationToken);
                return result.shown;
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] Interstitial failed: {e.Message}");
                return false;
            }
        }

        public async UniTask<RewardedAdResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default)
        {
            if (!IsRewardedAvailable)
                return RewardedAdResult.NotAvailable;

            try
            {
                var result = await YandexBridge.ShowRewardedAsync(cancellationToken);

                if (result.rewarded)
                    return RewardedAdResult.Rewarded;

                return result.shown ? RewardedAdResult.Closed : RewardedAdResult.NotAvailable;
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] Rewarded '{placement}' failed: {e.Message}");
                return RewardedAdResult.Failed;
            }
        }
    }
}
