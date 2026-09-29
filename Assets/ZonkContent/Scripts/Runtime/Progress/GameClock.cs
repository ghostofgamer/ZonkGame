using System;

namespace Zonk.Progress
{
    /// <summary>Местное время игрока для заданий. Отдельный интерфейс, чтобы время можно было подменить в тестах.</summary>
    public interface IGameClock
    {
        DateTime LocalNow { get; }
    }

    public sealed class SystemGameClock : IGameClock
    {
        public DateTime LocalNow => DateTime.Now;
    }
}
