namespace Base.Platform.Yandex
{
    /// <summary>
    /// Общее состояние сессии Яндекса: заполняется при инициализации и авторизации,
    /// читается всеми сервисами площадки.
    /// </summary>
    internal static class YandexSession
    {
        public static bool IsInitialized { get; private set; }
        public static string Language { get; private set; } = "ru";
        public static DeviceKind Device { get; private set; } = DeviceKind.Unknown;
        public static bool IsAuthorized { get; private set; }
        public static string PlayerId { get; private set; }
        public static string PlayerName { get; private set; }

        public static void Apply(PlayerInfoDto info)
        {
            if (info == null)
                return;

            IsInitialized = true;
            Language = string.IsNullOrEmpty(info.lang) ? "ru" : info.lang;
            Device = ParseDevice(info.device);
            IsAuthorized = info.isAuthorized;
            PlayerId = string.IsNullOrEmpty(info.playerId) ? null : info.playerId;
            PlayerName = string.IsNullOrEmpty(info.playerName) ? null : info.playerName;
        }

        private static DeviceKind ParseDevice(string type)
        {
            switch (type)
            {
                case "desktop": return DeviceKind.Desktop;
                case "mobile": return DeviceKind.Mobile;
                case "tablet": return DeviceKind.Tablet;
                case "tv": return DeviceKind.TV;
                default: return DeviceKind.Unknown;
            }
        }
    }
}
