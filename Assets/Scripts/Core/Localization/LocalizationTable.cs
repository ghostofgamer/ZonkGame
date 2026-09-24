using System.Collections.Generic;

namespace Base.Core.Localization
{
    /// <summary>
    /// Тексты интерфейса. Ключ, затем перевод на каждый поддерживаемый язык.
    /// Когда текстов станет много, таблицу заменит файл ресурсов, интерфейс останется тем же.
    /// </summary>
    public static class LocalizationTable
    {
        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Build()
        {
            var table = new Dictionary<string, IReadOnlyDictionary<string, string>>();

            Add(table, "language.name", "Русский", "English");

            // Кнопки тестовой панели
            Add(table, "btn.status", "Статус", "Status");
            Add(table, "btn.authorize", "Авторизация", "Sign in");
            Add(table, "btn.gameReady", "Игра готова", "Game ready");
            Add(table, "btn.gameplayStart", "Старт геймплея", "Gameplay start");
            Add(table, "btn.gameplayStop", "Стоп геймплея", "Gameplay stop");
            Add(table, "btn.adsStatus", "Реклама: доступность", "Ads: availability");
            Add(table, "btn.interstitial", "Показать interstitial", "Show interstitial");
            Add(table, "btn.rewarded", "Показать rewarded", "Show rewarded");
            Add(table, "btn.interstitialRules", "Interstitial по правилам", "Interstitial by rules");
            Add(table, "btn.reward", "Награда (реклама или даром)", "Reward (ad or free)");
            Add(table, "btn.products", "Магазин: товары", "Store: products");
            Add(table, "btn.buy", "Купить no_ads", "Buy no_ads");
            Add(table, "btn.entitlements", "Права игрока", "Player entitlements");
            Add(table, "btn.resetEntitlements", "Сбросить права", "Reset entitlements");
            Add(table, "btn.pending", "Неподтверждённые покупки", "Pending purchases");
            Add(table, "btn.consume", "Подтвердить все", "Consume all");
            Add(table, "btn.save", "Сохранить (+1)", "Save (+1)");
            Add(table, "btn.load", "Прочитать облако", "Read cloud");
            Add(table, "btn.submitScore", "Лидерборд: отправить очки", "Leaderboard: submit score");
            Add(table, "btn.top", "Лидерборд: топ 10", "Leaderboard: top 10");
            Add(table, "btn.myEntry", "Лидерборд: моя запись", "Leaderboard: my entry");
            Add(table, "btn.clearLog", "Очистить лог", "Clear log");
            Add(table, "btn.language", "Сменить язык", "Switch language");
            Add(table, "btn.quality", "Сменить качество", "Switch quality");

            // Сообщения
            Add(table, "log.adOpened", "Реклама открыта: звук выключен, пауза", "Ad opened: sound off, paused");
            Add(table, "log.adClosed", "Реклама закрыта: звук включён, пауза снята", "Ad closed: sound on, resumed");
            Add(table, "log.gameReadySent", "Сигнал готовности отправлен", "Ready signal sent");
            Add(table, "log.gameplayStartSent", "Старт геймплея отправлен", "Gameplay start sent");
            Add(table, "log.gameplayStopSent", "Стоп геймплея отправлен", "Gameplay stop sent");
            Add(table, "log.noSave", "Сохранения нет", "No save found");
            Add(table, "log.products", "Товаров", "Products");
            Add(table, "log.pending", "Неподтверждённых покупок", "Pending purchases");
            Add(table, "log.topEntries", "Записей в топе", "Top entries");
            Add(table, "log.noEntry", "Игрока нет в таблице", "Player is not in the leaderboard");
            Add(table, "log.me", "я", "me");
            Add(table, "log.languageDetected", "Язык определён по SDK", "Language detected from SDK");
            Add(table, "log.quality", "Качество", "Quality");

            return table;
        }

        private static void Add(
            IDictionary<string, IReadOnlyDictionary<string, string>> table,
            string key,
            string russian,
            string english)
        {
            table[key] = new Dictionary<string, string>
            {
                { Localization.Russian, russian },
                { Localization.English, english },
            };
        }
    }
}
