using Base.Platform;

namespace Base.Services.Analytics
{
    /// <summary>Аналитика по папкам поверх IAnalyticsService площадки. Подробности и пример — IAnalytics.</summary>
    public sealed class Analytics : IAnalytics
    {
        private readonly IAnalyticsService _service;

        public Analytics(IAnalyticsService service)
        {
            _service = service;
        }

        public bool IsAvailable => _service != null && _service.IsAvailable;

        public AnalyticsEvent Event(string folder)
        {
            return new AnalyticsEvent(_service, folder);
        }

        public void Track(string folder, params string[] path)
        {
            Event(folder).Path(path).Send();
        }

        public void SetUserProperty(string key, string value)
        {
            _service?.SetUserProperty(key, value);
        }

        public void SetUserProperty(string key, double value)
        {
            _service?.SetUserProperty(key, value);
        }

        public void Flush()
        {
            _service?.Flush();
        }
    }
}
