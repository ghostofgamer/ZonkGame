using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace Base.Platform.RuStore
{
    /// <summary>
    /// Реклама Android-сборки через Яндекс Рекламу (пакет com.yandex.mobileads 8.4.0 из OpenUPM).
    /// У RuStore своей рекламной сети нет.
    ///
    /// Оба формата загружаются заранее и перезагружаются после каждого показа, поэтому
    /// IsInterstitialAvailable / IsRewardedAvailable значат «реклама уже загружена и покажется сразу».
    /// При ошибке загрузки повтор через 30 секунд: Яндекс не советует перезапрашивать сразу из колбэка ошибки.
    /// Колбэки плагина приходят в главный поток Unity (у плагина свой MainThreadDispatcher).
    /// Идентификаторы блоков: RuStoreAdUnits.
    /// </summary>
    public sealed class RuStoreAdsService : IAdsService
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        private readonly InterstitialAdLoader _interstitialLoader;
        private readonly RewardedAdLoader _rewardedLoader;

        private Interstitial _interstitial;
        private RewardedAd _rewarded;
        private bool _interstitialLoading;
        private bool _rewardedLoading;
        private bool _showing;

        public RuStoreAdsService()
        {
            _interstitialLoader = new InterstitialAdLoader();
            _rewardedLoader = new RewardedAdLoader();

            LoadInterstitial();
            LoadRewarded();
        }

        public bool IsInterstitialAvailable => _interstitial != null;
        public bool IsRewardedAvailable => _rewarded != null;

        public event Action AdOpened;
        public event Action AdClosed;

        public async UniTask<bool> ShowInterstitialAsync(CancellationToken cancellationToken = default)
        {
            if (_showing)
                return false;

            var ad = _interstitial;
            if (ad == null)
            {
                Debug.LogWarning("[RuStore] Interstitial is not loaded yet");
                LoadInterstitial();
                return false;
            }

            _interstitial = null;
            _showing = true;

            var completion = new UniTaskCompletionSource<bool>();
            var opened = false;

            EventHandler<EventArgs> onShown = (_, __) =>
            {
                opened = true;
                AdOpened?.Invoke();
            };
            EventHandler<EventArgs> onDismissed = (_, __) => completion.TrySetResult(true);
            EventHandler<AdFailureEventArgs> onFailed = (_, args) =>
            {
                Debug.LogWarning($"[RuStore] Interstitial failed to show: {args.Message}");
                completion.TrySetResult(false);
            };

            ad.OnAdShown += onShown;
            ad.OnAdDismissed += onDismissed;
            ad.OnAdFailedToShow += onFailed;

            try
            {
                ad.Show();
                return await completion.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                ad.OnAdShown -= onShown;
                ad.OnAdDismissed -= onDismissed;
                ad.OnAdFailedToShow -= onFailed;
                ad.Destroy();

                if (opened)
                    AdClosed?.Invoke();

                _showing = false;
                LoadInterstitial();
            }
        }

        public async UniTask<RewardedAdResult> ShowRewardedAsync(string placement, CancellationToken cancellationToken = default)
        {
            if (_showing)
                return RewardedAdResult.NotAvailable;

            var ad = _rewarded;
            if (ad == null)
            {
                Debug.LogWarning($"[RuStore] Rewarded '{placement}' is not loaded yet");
                LoadRewarded();
                return RewardedAdResult.NotAvailable;
            }

            _rewarded = null;
            _showing = true;

            var completion = new UniTaskCompletionSource<bool>();
            var opened = false;
            var rewarded = false;

            EventHandler<EventArgs> onShown = (_, __) =>
            {
                opened = true;
                AdOpened?.Invoke();
            };
            EventHandler<Reward> onRewarded = (_, __) => rewarded = true;
            EventHandler<EventArgs> onDismissed = (_, __) => completion.TrySetResult(true);
            EventHandler<AdFailureEventArgs> onFailed = (_, args) =>
            {
                Debug.LogWarning($"[RuStore] Rewarded '{placement}' failed to show: {args.Message}");
                completion.TrySetResult(false);
            };

            ad.OnAdShown += onShown;
            ad.OnRewarded += onRewarded;
            ad.OnAdDismissed += onDismissed;
            ad.OnAdFailedToShow += onFailed;

            try
            {
                ad.Show();
                var shown = await completion.Task.AttachExternalCancellation(cancellationToken);

                if (rewarded)
                    return RewardedAdResult.Rewarded;

                return shown ? RewardedAdResult.Closed : RewardedAdResult.Failed;
            }
            finally
            {
                ad.OnAdShown -= onShown;
                ad.OnRewarded -= onRewarded;
                ad.OnAdDismissed -= onDismissed;
                ad.OnAdFailedToShow -= onFailed;
                ad.Destroy();

                if (opened)
                    AdClosed?.Invoke();

                _showing = false;
                LoadRewarded();
            }
        }

        private void LoadInterstitial()
        {
            if (_interstitial != null || _interstitialLoading)
                return;

            _interstitialLoading = true;
            _interstitialLoader.LoadAd(
                new AdRequest(RuStoreAdUnits.Interstitial),
                ad =>
                {
                    _interstitialLoading = false;
                    _interstitial = ad;
                    Debug.Log("[RuStore] Interstitial loaded");
                },
                args =>
                {
                    _interstitialLoading = false;
                    Debug.LogWarning($"[RuStore] Interstitial failed to load: {args.Message}");
                    RetryLater(LoadInterstitial).Forget();
                });
        }

        private void LoadRewarded()
        {
            if (_rewarded != null || _rewardedLoading)
                return;

            _rewardedLoading = true;
            _rewardedLoader.LoadAd(
                new AdRequest(RuStoreAdUnits.Rewarded),
                ad =>
                {
                    _rewardedLoading = false;
                    _rewarded = ad;
                    Debug.Log("[RuStore] Rewarded loaded");
                },
                args =>
                {
                    _rewardedLoading = false;
                    Debug.LogWarning($"[RuStore] Rewarded failed to load: {args.Message}");
                    RetryLater(LoadRewarded).Forget();
                });
        }

        private static async UniTaskVoid RetryLater(Action load)
        {
            await UniTask.Delay(RetryDelay, ignoreTimeScale: true);
            load();
        }
    }
}
