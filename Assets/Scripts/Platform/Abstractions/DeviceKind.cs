namespace Base.Platform
{
    /// <summary>
    /// Тип устройства игрока. Площадка сообщает его из SDK, по нему игра выбирает качество графики.
    /// Не путать с UnityEngine.DeviceType: там нет разделения на телефон и планшет.
    /// </summary>
    public enum DeviceKind
    {
        /// <summary>Площадка не сообщила тип устройства.</summary>
        Unknown = 0,
        Desktop = 1,
        Mobile = 2,
        Tablet = 3,
        TV = 4,
    }
}
