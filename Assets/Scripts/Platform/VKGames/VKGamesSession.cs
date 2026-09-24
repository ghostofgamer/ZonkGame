using System;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Общее состояние сессии Игр ВКонтакте: заполняется при инициализации,
    /// читается всеми сервисами площадки.
    /// </summary>
    internal static class VKGamesSession
    {
        public static bool IsInitialized { get; private set; }
        public static string Language { get; private set; } = "ru";
        public static DeviceKind Device { get; private set; } = DeviceKind.Unknown;
        public static string VkPlatform { get; private set; }
        public static string UserId { get; private set; }
        public static string UserName { get; private set; }

        public static void Apply(LaunchInfoDto info)
        {
            if (info == null)
                return;

            IsInitialized = true;
            Language = string.IsNullOrEmpty(info.lang) ? "ru" : info.lang;
            VkPlatform = string.IsNullOrEmpty(info.platform) ? null : info.platform;
            Device = ParseDevice(VkPlatform);
            UserId = string.IsNullOrEmpty(info.userId) ? null : info.userId;
            UserName = string.IsNullOrEmpty(info.userName) ? null : info.userName;
        }

        /// <summary>
        /// vk_platform: desktop_web, desktop_*_messenger, mobile_web, mobile_android, mobile_iphone,
        /// mobile_ipad, mobile_*_messenger. Отдельно из мобильных выделяется только iPad.
        /// </summary>
        private static DeviceKind ParseDevice(string vkPlatform)
        {
            if (string.IsNullOrEmpty(vkPlatform))
                return DeviceKind.Unknown;

            if (vkPlatform == "mobile_ipad")
                return DeviceKind.Tablet;

            if (vkPlatform.StartsWith("desktop", StringComparison.Ordinal))
                return DeviceKind.Desktop;

            if (vkPlatform.StartsWith("mobile", StringComparison.Ordinal))
                return DeviceKind.Mobile;

            return DeviceKind.Unknown;
        }
    }
}
