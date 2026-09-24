using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Yandex
{
    /// <summary>Ошибка вызова Yandex Games SDK. Текст приходит из JS.</summary>
    public sealed class YandexBridgeException : Exception
    {
        public YandexBridgeException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Точка обмена с YandexBridge.jslib. Каждый вызов получает номер запроса,
    /// JS отвечает через SendMessage на объект YandexSdkBridge, ответ находит свой
    /// UniTaskCompletionSource по номеру.
    /// Вне WebGL-билда все вызовы завершаются ошибкой: SDK там не существует.
    /// </summary>
    internal static class YandexBridge
    {
        /// <summary>Имя GameObject должно совпадать с RECEIVER в YandexBridge.jslib.</summary>
        private const string ReceiverName = "YandexSdkBridge";

        private static readonly Dictionary<int, UniTaskCompletionSource<string>> Pending =
            new Dictionary<int, UniTaskCompletionSource<string>>();

        private static int _nextRequestId = 1;
        private static YandexBridgeReceiver _receiver;

        /// <summary>Реклама открылась: игре нужно заглушить звук и встать на паузу.</summary>
        public static event Action AdOpened;

        /// <summary>Реклама закрылась.</summary>
        public static event Action AdClosed;

        public static bool IsSupported
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            get { return true; }
#else
            get { return false; }
#endif
        }

        // ---------- Публичные вызовы ----------

        public static async UniTask<PlayerInfoDto> InitializeAsync(CancellationToken cancellationToken)
        {
            EnsureReceiver();
            var json = await CallAsync(id => YandexBridgeInit(id), cancellationToken);
            return JsonUtility.FromJson<PlayerInfoDto>(json);
        }

        public static void NotifyGameReady()
        {
            if (IsSupported) YandexBridgeGameReady();
        }

        public static void NotifyGameplayStart()
        {
            if (IsSupported) YandexBridgeGameplayStart();
        }

        public static void NotifyGameplayStop()
        {
            if (IsSupported) YandexBridgeGameplayStop();
        }

        public static async UniTask<PlayerInfoDto> AuthorizeAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgeAuthorize(id), cancellationToken);
            return JsonUtility.FromJson<PlayerInfoDto>(json);
        }

        public static async UniTask<InterstitialResultDto> ShowInterstitialAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgeShowInterstitial(id), cancellationToken);
            return JsonUtility.FromJson<InterstitialResultDto>(json);
        }

        public static async UniTask<RewardedResultDto> ShowRewardedAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgeShowRewarded(id), cancellationToken);
            return JsonUtility.FromJson<RewardedResultDto>(json);
        }

        public static UniTask InitPaymentsAsync(CancellationToken cancellationToken)
        {
            return CallAsync(id => YandexBridgeInitPayments(id), cancellationToken);
        }

        public static async UniTask<ProductListDto> GetCatalogAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgeGetCatalog(id), cancellationToken);
            return JsonUtility.FromJson<ProductListDto>(json);
        }

        public static async UniTask<PurchaseDto> PurchaseAsync(string productId, CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgePurchase(id, productId), cancellationToken);
            return JsonUtility.FromJson<PurchaseDto>(json);
        }

        public static async UniTask<PurchaseListDto> GetPurchasesAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgeGetPurchases(id), cancellationToken);
            return JsonUtility.FromJson<PurchaseListDto>(json);
        }

        public static UniTask ConsumeAsync(string purchaseToken, CancellationToken cancellationToken)
        {
            return CallAsync(id => YandexBridgeConsume(id, purchaseToken), cancellationToken);
        }

        public static UniTask SaveAsync(string json, CancellationToken cancellationToken)
        {
            return CallAsync(id => YandexBridgeSave(id, json), cancellationToken);
        }

        public static async UniTask<string> LoadAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgeLoad(id), cancellationToken);
            var data = JsonUtility.FromJson<SaveDataDto>(json);
            return string.IsNullOrEmpty(data.json) ? null : data.json;
        }

        public static UniTask SubmitScoreAsync(string boardId, long score, CancellationToken cancellationToken)
        {
            return CallAsync(id => YandexBridgeSubmitScore(id, boardId, score), cancellationToken);
        }

        public static async UniTask<EntryListDto> GetTopAsync(string boardId, int count, CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgeGetTop(id, boardId, count), cancellationToken);
            return JsonUtility.FromJson<EntryListDto>(json);
        }

        public static async UniTask<PlayerEntryDto> GetPlayerEntryAsync(string boardId, CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => YandexBridgeGetPlayerEntry(id, boardId), cancellationToken);
            return JsonUtility.FromJson<PlayerEntryDto>(json);
        }

        // ---------- Механика запросов ----------

        /// <summary>Отправляет вызов в JS и ждёт ответ с тем же номером запроса.</summary>
        private static async UniTask<string> CallAsync(Action<int> call, CancellationToken cancellationToken)
        {
            if (!IsSupported)
                throw new YandexBridgeException("Yandex SDK is available only in a WebGL build");

            EnsureReceiver();

            var requestId = _nextRequestId++;
            var completion = new UniTaskCompletionSource<string>();
            Pending[requestId] = completion;

            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    if (Pending.Remove(requestId))
                        completion.TrySetCanceled(cancellationToken);
                });
            }

            try
            {
                call(requestId);
                return await completion.Task;
            }
            catch (Exception)
            {
                Pending.Remove(requestId);
                throw;
            }
            finally
            {
                registration.Dispose();
            }
        }

        /// <summary>Вызывается из YandexBridgeReceiver, когда JS присылает ответ.</summary>
        internal static void HandleResponse(string payload)
        {
            BridgeResponseDto response;
            try
            {
                response = JsonUtility.FromJson<BridgeResponseDto>(payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Yandex] Cannot parse bridge response: {e.Message}\n{payload}");
                return;
            }

            if (!Pending.TryGetValue(response.id, out var completion))
            {
                // Запрос уже отменён: ответ пришёл после отмены, это нормально.
                return;
            }

            Pending.Remove(response.id);

            if (response.ok)
                completion.TrySetResult(response.result);
            else
                completion.TrySetException(new YandexBridgeException(response.error));
        }

        /// <summary>Вызывается из YandexBridgeReceiver для событий рекламы.</summary>
        internal static void HandleEvent(string eventName)
        {
            switch (eventName)
            {
                case "adOpened":
                    AdOpened?.Invoke();
                    break;
                case "adClosed":
                    AdClosed?.Invoke();
                    break;
                default:
                    Debug.LogWarning($"[Yandex] Unknown bridge event: {eventName}");
                    break;
            }
        }

        /// <summary>
        /// Объект-приёмник должен существовать до первого вызова: SendMessage из JS
        /// ищет его по имени и молча теряет сообщение, если объекта нет.
        /// </summary>
        private static void EnsureReceiver()
        {
            if (_receiver != null)
                return;

            var go = new GameObject(ReceiverName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _receiver = go.AddComponent<YandexBridgeReceiver>();
        }

        // ---------- Импорт из jslib ----------

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void YandexBridgeInit(int requestId);
        [DllImport("__Internal")] private static extern void YandexBridgeGameReady();
        [DllImport("__Internal")] private static extern void YandexBridgeGameplayStart();
        [DllImport("__Internal")] private static extern void YandexBridgeGameplayStop();
        [DllImport("__Internal")] private static extern void YandexBridgeAuthorize(int requestId);
        [DllImport("__Internal")] private static extern void YandexBridgeShowInterstitial(int requestId);
        [DllImport("__Internal")] private static extern void YandexBridgeShowRewarded(int requestId);
        [DllImport("__Internal")] private static extern void YandexBridgeInitPayments(int requestId);
        [DllImport("__Internal")] private static extern void YandexBridgeGetCatalog(int requestId);
        [DllImport("__Internal")] private static extern void YandexBridgePurchase(int requestId, string productId);
        [DllImport("__Internal")] private static extern void YandexBridgeGetPurchases(int requestId);
        [DllImport("__Internal")] private static extern void YandexBridgeConsume(int requestId, string purchaseToken);
        [DllImport("__Internal")] private static extern void YandexBridgeSave(int requestId, string json);
        [DllImport("__Internal")] private static extern void YandexBridgeLoad(int requestId);
        [DllImport("__Internal")] private static extern void YandexBridgeSubmitScore(int requestId, string boardId, double score);
        [DllImport("__Internal")] private static extern void YandexBridgeGetTop(int requestId, string boardId, int count);
        [DllImport("__Internal")] private static extern void YandexBridgeGetPlayerEntry(int requestId, string boardId);
#else
        // Заглушки для редактора: сюда попадать не должны, CallAsync отсекает раньше.
        private static void YandexBridgeInit(int requestId) { }
        private static void YandexBridgeGameReady() { }
        private static void YandexBridgeGameplayStart() { }
        private static void YandexBridgeGameplayStop() { }
        private static void YandexBridgeAuthorize(int requestId) { }
        private static void YandexBridgeShowInterstitial(int requestId) { }
        private static void YandexBridgeShowRewarded(int requestId) { }
        private static void YandexBridgeInitPayments(int requestId) { }
        private static void YandexBridgeGetCatalog(int requestId) { }
        private static void YandexBridgePurchase(int requestId, string productId) { }
        private static void YandexBridgeGetPurchases(int requestId) { }
        private static void YandexBridgeConsume(int requestId, string purchaseToken) { }
        private static void YandexBridgeSave(int requestId, string json) { }
        private static void YandexBridgeLoad(int requestId) { }
        private static void YandexBridgeSubmitScore(int requestId, string boardId, double score) { }
        private static void YandexBridgeGetTop(int requestId, string boardId, int count) { }
        private static void YandexBridgeGetPlayerEntry(int requestId, string boardId) { }
#endif
    }
}
