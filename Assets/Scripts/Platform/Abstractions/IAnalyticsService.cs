namespace Base.Platform
{
    /// <summary>
    /// Аналитика площадки: события и свойства игрока. Низкий уровень — игра пользуется Base.Services.Analytics.IAnalytics
    /// (события по «папкам»), а не этим интерфейсом напрямую.
    /// RuStore (Android) — AppMetrica. Яндекс Игры и VK — пока нет (позже Яндекс Метрика через JS), вызовы ничего не делают.
    /// </summary>
    public interface IAnalyticsService
    {
        bool IsAvailable { get; }

        /// <summary>Событие name с параметрами в JSON (вложенность объектов до 5 уровней) или без них (null).</summary>
        void ReportEvent(string name, string parametersJson);

        /// <summary>Свойство игрока (строка) для разрезов в отчётах: площадка, глава, платящий.</summary>
        void SetUserProperty(string key, string value);

        /// <summary>Свойство игрока (число).</summary>
        void SetUserProperty(string key, double value);

        /// <summary>Отправить накопленные события сейчас (перед выходом, после важного события).</summary>
        void Flush();
    }
}
