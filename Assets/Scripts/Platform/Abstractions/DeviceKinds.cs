using UnityEngine;

namespace Base.Platform
{
    public static class DeviceKinds
    {
        /// <summary>
        /// Запасной вариант, когда SDK не сообщил тип устройства.
        /// В WebGL Unity возвращает isMobilePlatform = true для мобильного браузера,
        /// но телефон от планшета не отличает.
        /// </summary>
        public static DeviceKind FromUnity()
        {
            return Application.isMobilePlatform ? DeviceKind.Mobile : DeviceKind.Desktop;
        }
    }
}
