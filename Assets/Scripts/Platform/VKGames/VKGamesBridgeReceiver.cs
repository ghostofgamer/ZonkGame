using UnityEngine;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Приёмник сообщений из JavaScript. Имя GameObject и имена методов
    /// жёстко связаны с VKGamesBridge.jslib: SendMessage ищет их по строкам.
    /// Объект создаётся автоматически в VKGamesBridge.EnsureReceiver.
    /// </summary>
    internal sealed class VKGamesBridgeReceiver : MonoBehaviour
    {
        // ReSharper disable once UnusedMember.Global - вызывается из JS
        public void OnBridgeResponse(string payload)
        {
            VKGamesBridge.HandleResponse(payload);
        }

        // ReSharper disable once UnusedMember.Global - вызывается из JS
        public void OnBridgeEvent(string eventName)
        {
            VKGamesBridge.HandleEvent(eventName);
        }
    }
}
