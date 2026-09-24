using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKGames
{
    /// <summary>Ошибка вызова VK Bridge. Текст приходит из JS.</summary>
    public sealed class VKGamesBridgeException : Exception
    {
        public VKGamesBridgeException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Точка обмена с VKGamesBridge.jslib. Каждый вызов получает номер запроса,
    /// JS отвечает через SendMessage на объект VKGamesSdkBridge, ответ находит свой
    /// UniTaskCompletionSource по номеру. Устроено так же, как YandexBridge.
    /// Вне WebGL-билда все вызовы завершаются ошибкой: VK Bridge там не существует.
    /// </summary>
    internal static class VKGamesBridge
    {
        /// <summary>Имя GameObject должно совпадать с RECEIVER в VKGamesBridge.jslib.</summary>
        private const string ReceiverName = "VKGamesSdkBridge";

        private const string InterstitialFormat = "interstitial";
        private const string RewardedFormat = "reward";

        private static readonly Dictionary<int, UniTaskCompletionSource<string>> Pending =
            new Dictionary<int, UniTaskCompletionSource<string>>();

        private static int _nextRequestId = 1;
        private static VKGamesBridgeReceiver _receiver;

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

        public static async UniTask<LaunchInfoDto> InitializeAsync(CancellationToken cancellationToken)
        {
            EnsureReceiver();
            var json = await CallAsync(id => VKGamesBridgeInit(id), cancellationToken);
            return JsonUtility.FromJson<LaunchInfoDto>(json);
        }

        public static async UniTask<AdResultDto> ShowInterstitialAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => VKGamesBridgeShowAd(id, InterstitialFormat), cancellationToken);
            return JsonUtility.FromJson<AdResultDto>(json);
        }

        public static async UniTask<AdResultDto> ShowRewardedAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => VKGamesBridgeShowAd(id, RewardedFormat), cancellationToken);
            return JsonUtility.FromJson<AdResultDto>(json);
        }

        /// <summary>Реклама уже предзагружена мостом и покажется без задержки.</summary>
        public static bool IsInterstitialReady => IsSupported && VKGamesBridgeIsAdReady(InterstitialFormat) != 0;

        public static bool IsRewardedReady => IsSupported && VKGamesBridgeIsAdReady(RewardedFormat) != 0;

        public static UniTask SaveAsync(string json, CancellationToken cancellationToken)
        {
            return CallAsync(id => VKGamesBridgeSave(id, json), cancellationToken);
        }

        public static async UniTask<string> LoadAsync(CancellationToken cancellationToken)
        {
            var json = await CallAsync(id => VKGamesBridgeLoad(id), cancellationToken);
            var data = JsonUtility.FromJson<SaveDataDto>(json);
            return string.IsNullOrEmpty(data.json) ? null : data.json;
        }

        // ---------- Механика запросов ----------

        /// <summary>Отправляет вызов в JS и ждёт ответ с тем же номером запроса.</summary>
        private static async UniTask<string> CallAsync(Action<int> call, CancellationToken cancellationToken)
        {
            if (!IsSupported)
                throw new VKGamesBridgeException("VK Bridge is available only in a WebGL build");

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

        /// <summary>Вызывается из VKGamesBridgeReceiver, когда JS присылает ответ.</summary>
        internal static void HandleResponse(string payload)
        {
            BridgeResponseDto response;
            try
            {
                response = JsonUtility.FromJson<BridgeResponseDto>(payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VKGames] Cannot parse bridge response: {e.Message}\n{payload}");
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
                completion.TrySetException(new VKGamesBridgeException(response.error));
        }

        /// <summary>Вызывается из VKGamesBridgeReceiver для событий рекламы.</summary>
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
                    Debug.LogWarning($"[VKGames] Unknown bridge event: {eventName}");
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
            _receiver = go.AddComponent<VKGamesBridgeReceiver>();
        }

        // ---------- Импорт из jslib ----------

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void VKGamesBridgeInit(int requestId);
        [DllImport("__Internal")] private static extern void VKGamesBridgeShowAd(int requestId, string format);
        [DllImport("__Internal")] private static extern int VKGamesBridgeIsAdReady(string format);
        [DllImport("__Internal")] private static extern void VKGamesBridgeSave(int requestId, string json);
        [DllImport("__Internal")] private static extern void VKGamesBridgeLoad(int requestId);
#else
        // Заглушки для редактора: сюда попадать не должны, CallAsync отсекает раньше.
        private static void VKGamesBridgeInit(int requestId) { }
        private static void VKGamesBridgeShowAd(int requestId, string format) { }
        private static int VKGamesBridgeIsAdReady(string format) { return 0; }
        private static void VKGamesBridgeSave(int requestId, string json) { }
        private static void VKGamesBridgeLoad(int requestId) { }
#endif
    }
}
