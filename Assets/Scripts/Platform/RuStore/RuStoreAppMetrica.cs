using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Io.AppMetrica;
#endif

namespace Base.Platform.RuStore
{
    /// <summary>
    /// Запуск AppMetrica в Android-сборке RuStore. Активация — до загрузки первой сцены (так советует AppMetrica:
    /// сессии и падения считаются с самого старта), поэтому не через Zenject, а RuntimeInitializeOnLoadMethod.
    ///
    /// Ключ: appmetrica.yandex.ru → «Добавить приложение» → «Настройки» → «API key». Ключ не секретный
    /// (он в любом случае лежит внутри APK), его можно хранить в репозитории. Пустой ключ — AppMetrica не запускается,
    /// события игры просто не отправляются.
    /// </summary>
    public static class RuStoreAppMetrica
    {
        /// <summary>API key приложения в AppMetrica. Своё у каждой игры (сейчас — Зонк; в шаблоне оставить пустым).</summary>
        public const string ApiKey = "80495263-a5b5-419e-a8e9-64cef1024778";

        /// <summary>AppMetrica запущена: можно отправлять события.</summary>
        public static bool IsActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Activate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(ApiKey))
            {
                Debug.LogWarning("[AppMetrica] ApiKey is empty (RuStoreAppMetrica.ApiKey): analytics is off");
                return;
            }

            AppMetrica.Activate(new AppMetricaConfig(ApiKey)
            {
                AppVersion = Application.version,
                CrashReporting = true,
                NativeCrashReporting = true,
                SessionsAutoTrackingEnabled = true,
#if DEVELOPMENT_BUILD
                Logs = true,
#endif
            });
            IsActive = true;
#endif
        }
    }
}
