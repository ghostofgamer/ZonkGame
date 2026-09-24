using System;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Классы для разбора ответов моста через JsonUtility.
    /// Имена полей должны совпадать с тем, что кладёт VKGamesBridge.jslib.
    /// </summary>
    [Serializable]
    internal sealed class BridgeResponseDto
    {
        public int id;
        public bool ok;
        /// <summary>Полезная нагрузка, сама по себе строка с JSON.</summary>
        public string result;
        public string error;
    }

    [Serializable]
    internal sealed class LaunchInfoDto
    {
        /// <summary>vk_language из параметров запуска: ru, uk, be, en и другие.</summary>
        public string lang;
        /// <summary>vk_platform из параметров запуска: desktop_web, mobile_web, mobile_android и другие.</summary>
        public string platform;
        public string userId;
        public string userName;
    }

    [Serializable]
    internal sealed class AdResultDto
    {
        public bool shown;
        /// <summary>Почему реклама не показана. Пусто, если показана.</summary>
        public string reason;
    }

    [Serializable]
    internal sealed class SaveDataDto
    {
        public string json;
    }
}
