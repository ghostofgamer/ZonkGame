namespace Base.Services.Haptics
{
    /// <summary>
    /// Короткая вибрация телефона: отклик на удары, броски, события игры.
    /// Работает в Android-сборке. В браузере (Яндекс Игры, VK) игра открывается во фрейме другого домена,
    /// а там браузеры вибрацию запрещают, поэтому на web и в редакторе IsSupported = false и вызовы ничего не делают.
    /// Включена ли вибрация у игрока, решает игра (своя настройка), частоту вызовов — тоже игра.
    /// </summary>
    public interface IHapticsService
    {
        bool IsSupported { get; }

        /// <summary>Импульс длиной milliseconds. amplitude 1..255, -1 — сила по умолчанию устройства.</summary>
        void Pulse(int milliseconds, int amplitude = -1);
    }
}
