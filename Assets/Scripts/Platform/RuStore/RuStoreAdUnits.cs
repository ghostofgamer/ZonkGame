namespace Base.Platform.RuStore
{
    /// <summary>
    /// Рекламные блоки Яндекс Рекламы для Android-сборки.
    ///
    /// Сейчас демо-блоки: они не требуют регистрации и всегда отдают тестовую рекламу.
    /// Перед публикацией заменить на свои блоки вида R-M-XXXXXX-Y из кабинета Рекламной сети Яндекса
    /// (partner.yandex.ru): там заводится приложение и по блоку на каждый формат.
    /// </summary>
    internal static class RuStoreAdUnits
    {
        public const string Interstitial = "demo-interstitial-yandex";
        public const string Rewarded = "demo-rewarded-yandex";
    }
}
