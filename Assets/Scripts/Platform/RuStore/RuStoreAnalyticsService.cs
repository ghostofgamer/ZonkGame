#if UNITY_ANDROID && !UNITY_EDITOR
using Io.AppMetrica;
using Io.AppMetrica.Profile;
#endif

namespace Base.Platform.RuStore
{
    /// <summary>
    /// Аналитика RuStore через AppMetrica. Запуск — RuStoreAppMetrica (до первой сцены). События уходят пакетами
    /// раз в несколько секунд и при сворачивании игры: отдельно Flush звать не нужно, только перед выходом.
    /// </summary>
    public sealed class RuStoreAnalyticsService : IAnalyticsService
    {
        public bool IsAvailable => RuStoreAppMetrica.IsActive;

        public void ReportEvent(string name, string parametersJson)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAvailable || string.IsNullOrEmpty(name))
                return;

            if (string.IsNullOrEmpty(parametersJson))
                AppMetrica.ReportEvent(name);
            else
                AppMetrica.ReportEvent(name, parametersJson);
#endif
        }

        public void SetUserProperty(string key, string value)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAvailable || string.IsNullOrEmpty(key))
                return;

            var update = value == null
                ? Attribute.CustomString(key).WithValueReset()
                : Attribute.CustomString(key).WithValue(value);
            AppMetrica.ReportUserProfile(new UserProfile().Apply(update));
#endif
        }

        public void SetUserProperty(string key, double value)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAvailable || string.IsNullOrEmpty(key))
                return;

            AppMetrica.ReportUserProfile(new UserProfile().Apply(Attribute.CustomNumber(key).WithValue(value)));
#endif
        }

        public void Flush()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsAvailable)
                AppMetrica.SendEventsBuffer();
#endif
        }
    }
}
