using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Stub
{
    /// <summary>
    /// Имитация рекламы: короткая задержка вместо ролика, награда выдаётся всегда.
    /// Через SimulateUnavailable / SimulateClose можно проверить ветки отказов.
    /// </summary>
    public sealed class StubAdsService : IAdsService
    {
        public static bool SimulateUnavailable;
        public static bool SimulateClose;
        public static float FakeAdSeconds = 0.5f;

        public bool IsInterstitialAvailable => !SimulateUnavailable;
        public bool IsRewardedAvailable => !SimulateUnavailable;

        public event Action AdOpened;
        public event Action AdClosed;

        public async UniTask<bool> ShowInterstitialAsync(CancellationToken cancellationToken = default)
        {
            if (!IsInterstitialAvailable)
            {
                Debug.Log("[Stub] Interstitial not available");
                return false;
            }

            Debug.Log("[Stub] Interstitial opened");
            AdOpened?.Invoke();
            await UniTask.Delay(TimeSpan.FromSeconds(FakeAdSeconds), ignoreTimeScale: true, cancellationToken: cancellationToken);
            AdClosed?.Invoke();
            Debug.Log("[Stub] Interstitial closed");
            return true;
        }

        public async UniTask<RewardedAdResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default)
        {
            if (!IsRewardedAvailable)
            {
                Debug.Log($"[Stub] Rewarded '{placement}' not available");
                return RewardedAdResult.NotAvailable;
            }

            Debug.Log($"[Stub] Rewarded '{placement}' opened");
            AdOpened?.Invoke();
            await UniTask.Delay(TimeSpan.FromSeconds(FakeAdSeconds), ignoreTimeScale: true, cancellationToken: cancellationToken);
            AdClosed?.Invoke();

            var result = SimulateClose ? RewardedAdResult.Closed : RewardedAdResult.Rewarded;
            Debug.Log($"[Stub] Rewarded '{placement}' -> {result}");
            return result;
        }
    }
}
