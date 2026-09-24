using System;
using Base.Platform;

namespace Base.Services.Quality
{
    /// <summary>
    /// Качество графики. Уровень выбирается автоматически по типу устройства,
    /// который сообщает площадка, и может быть изменён вручную (например, из меню настроек).
    /// </summary>
    public interface IQualityService
    {
        QualityTier Tier { get; }

        /// <summary>Уровень, который подходит устройству. Меняется только в ApplyForDevice.</summary>
        QualityTier Recommended { get; }

        /// <summary>Тип устройства, по которому выбран рекомендованный уровень.</summary>
        DeviceKind Device { get; }

        event Action TierChanged;

        /// <summary>Выбрать и применить уровень по типу устройства. Вызывается после инициализации площадки.</summary>
        void ApplyForDevice(DeviceKind device);

        /// <summary>Применить уровень вручную.</summary>
        void SetTier(QualityTier tier);
    }
}
