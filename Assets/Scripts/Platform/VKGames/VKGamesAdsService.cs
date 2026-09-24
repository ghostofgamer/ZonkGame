using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Нативная реклама VK: VKWebAppShowNativeAds с форматами interstitial и reward.
    /// Мост предзагружает оба формата после инициализации и раз в 30 секунд (VKWebAppCheckNativeAds).
    ///
    /// Правила VK: rewarded только по действию игрока и с понятной наградой, а кнопку показа
    /// выводить, только когда реклама готова (IsRewardedAvailable). Interstitial только на переходах
    /// между экранами и никогда сразу после запуска игры. До прохождения модерации реклама работает в тестовом режиме.
    /// </summary>
    public sealed class VKGamesAdsService : IAdsService
    {
        public VKGamesAdsService()
        {
            VKGamesBridge.AdOpened += () => AdOpened?.Invoke();
            VKGamesBridge.AdClosed += () => AdClosed?.Invoke();
        }

        /// <summary>Interstitial можно запрашивать и без предзагрузки: VK догрузит рекламу сам.</summary>
        public bool IsInterstitialAvailable => IsReady;

        /// <summary>Rewarded считается доступной, только когда предзагружена: так требует VK для кнопки показа.</summary>
        public bool IsRewardedAvailable => IsReady && VKGamesBridge.IsRewardedReady;

        public event Action AdOpened;
        public event Action AdClosed;

        private static bool IsReady => VKGamesBridge.IsSupported && VKGamesSession.IsInitialized;

        public async UniTask<bool> ShowInterstitialAsync(CancellationToken cancellationToken = default)
        {
            if (!IsReady)
                return false;

            try
            {
                var result = await VKGamesBridge.ShowInterstitialAsync(cancellationToken);
                if (!result.shown)
                    Debug.LogWarning($"[VKGames] Interstitial not shown: {result.reason}");
                return result.shown;
            }
            catch (VKGamesBridgeException e)
            {
                Debug.LogWarning($"[VKGames] Interstitial failed: {e.Message}");
                return false;
            }
        }

        public async UniTask<RewardedAdResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default)
        {
            // Показ пробуем, даже если предзагрузка не успела: VK может догрузить рекламу при показе.
            if (!IsReady)
                return RewardedAdResult.NotAvailable;

            try
            {
                // VK не сообщает отдельно, досмотрел ли игрок рекламу: result: true единственный признак успеха.
                // Что приходит при закрытии раньше времени, документация не описывает: проверить на живой площадке.
                var result = await VKGamesBridge.ShowRewardedAsync(cancellationToken);
                if (result.shown)
                    return RewardedAdResult.Rewarded;

                Debug.LogWarning($"[VKGames] Rewarded '{placement}' not shown: {result.reason}");
                return RewardedAdResult.NotAvailable;
            }
            catch (VKGamesBridgeException e)
            {
                Debug.LogWarning($"[VKGames] Rewarded '{placement}' failed: {e.Message}");
                return RewardedAdResult.Failed;
            }
        }
    }
}
