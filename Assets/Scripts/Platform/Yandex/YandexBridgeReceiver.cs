using UnityEngine;

namespace Base.Platform.Yandex
{
    /// <summary>
    /// Приёмник сообщений из JavaScript. Имя GameObject и имена методов
    /// жёстко связаны с YandexBridge.jslib: SendMessage ищет их по строкам.
    /// Объект создаётся автоматически в YandexBridge.EnsureReceiver.
    /// </summary>
    internal sealed class YandexBridgeReceiver : MonoBehaviour
    {
        // ReSharper disable once UnusedMember.Global - вызывается из JS
        public void OnBridgeResponse(string payload)
        {
            YandexBridge.HandleResponse(payload);
        }

        // ReSharper disable once UnusedMember.Global - вызывается из JS
        public void OnBridgeEvent(string eventName)
        {
            YandexBridge.HandleEvent(eventName);
        }
    }
}
