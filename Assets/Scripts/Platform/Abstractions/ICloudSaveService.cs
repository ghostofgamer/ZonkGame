using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Platform
{
    /// <summary>
    /// Облачное сохранение. Один JSON-блоб на игрока.
    /// Сериализацию делает игра, площадка только хранит строку.
    /// </summary>
    public interface ICloudSaveService
    {
        bool IsAvailable { get; }

        UniTask SaveAsync(string json, CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает null, если сохранения ещё нет. Если прочитать не удалось, бросает исключение:
        /// сбой нельзя путать с пустым сохранением, иначе прогресс игрока затрётся.
        /// </summary>
        UniTask<string> LoadAsync(CancellationToken cancellationToken = default);
    }
}
