using UnityEngine;

namespace Base.Platform.Stub
{
    /// <summary>
    /// Аналитика без SDK: в редакторе и отладочной сборке пишет события в консоль (так удобно проверять, что и когда
    /// игра отправляет), в обычной сборке молчит. Используется в редакторе и на площадках без аналитики (Яндекс, VK).
    /// </summary>
    public sealed class StubAnalyticsService : IAnalyticsService
    {
        public bool IsAvailable => false;

        public void ReportEvent(string name, string parametersJson)
        {
            Log("event '" + name + "' " + parametersJson);
        }

        public void SetUserProperty(string key, string value)
        {
            Log("user " + key + " = " + value);
        }

        public void SetUserProperty(string key, double value)
        {
            Log("user " + key + " = " + value);
        }

        public void Flush()
        {
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void Log(string message)
        {
            Debug.Log("[Analytics] " + message);
        }
    }
}
