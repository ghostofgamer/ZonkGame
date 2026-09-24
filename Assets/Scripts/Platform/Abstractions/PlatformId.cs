namespace Base.Platform
{
    /// <summary>Целевая площадка, под которую собран билд.</summary>
    public enum PlatformId
    {
        /// <summary>Заглушка: редактор и локальные тесты без SDK.</summary>
        Stub = 0,
        /// <summary>Яндекс Игры, WebGL.</summary>
        Yandex = 1,
        /// <summary>VK Play (vkplay.ru), раздел браузерных игр, WebGL. Свой API, не VK Bridge.</summary>
        VKPlay = 2,
        /// <summary>RuStore, нативный Android.</summary>
        RuStore = 3,
        /// <summary>Игры ВКонтакте (vk.com и мобильное приложение VK), WebGL через VK Bridge.</summary>
        VKGames = 4,
    }
}
