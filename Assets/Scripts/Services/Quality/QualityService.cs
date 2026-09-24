using System;
using Base.Platform;
using UnityEngine;

namespace Base.Services.Quality
{
    /// <summary>
    /// Переключает Quality Level Unity по уровню качества.
    ///
    /// Код только выбирает уровень. Что на нём меняется, задаётся в Project Settings > Quality:
    /// URP-ассет уровня (render scale, тени, сглаживание), Global Mipmap Limit (ужатие текстур),
    /// LOD bias и прочее. Так настройки качества правятся без изменения кода.
    ///
    /// Если уровня с нужным именем нет на текущей платформе (на Android нет "PC"),
    /// берётся ближайший уровень ниже.
    /// </summary>
    public sealed class QualityService : IQualityService
    {
        public QualityTier Tier { get; private set; } = QualityTier.Low;
        public QualityTier Recommended { get; private set; } = QualityTier.Low;
        public DeviceKind Device { get; private set; } = DeviceKind.Unknown;

        public event Action TierChanged;

        public void ApplyForDevice(DeviceKind device)
        {
            Device = device;
            Recommended = SelectFor(device);
            Debug.Log($"[Quality] Device {device} -> recommended tier {Recommended}");

            // TODO: когда появится меню настроек, сохранённый выбор игрока должен побеждать рекомендацию.
            SetTier(Recommended);
        }

        public void SetTier(QualityTier tier)
        {
            Tier = tier;
            ApplyLevel(tier);
            TierChanged?.Invoke();
        }

        /// <summary>Какой уровень подходит устройству.</summary>
        public static QualityTier SelectFor(DeviceKind device)
        {
            switch (device)
            {
                case DeviceKind.Desktop:
                case DeviceKind.TV:
                    return QualityTier.High;
                case DeviceKind.Tablet:
                    return QualityTier.Medium;
                default:
                    return QualityTier.Low;
            }
        }

        /// <summary>
        /// Имя Quality Level в Project Settings для уровня качества.
        /// Сейчас в проекте два уровня из шаблона URP: "Mobile" и "PC".
        /// Уровня "Medium" нет, поэтому планшеты пока получают "Mobile".
        /// </summary>
        private static string LevelNameFor(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.High: return "PC";
                case QualityTier.Medium: return "Medium";
                default: return "Mobile";
            }
        }

        private static void ApplyLevel(QualityTier tier)
        {
            var names = QualitySettings.names;

            for (var t = (int)tier; t >= 0; t--)
            {
                var levelName = LevelNameFor((QualityTier)t);
                var index = Array.IndexOf(names, levelName);
                if (index < 0)
                    continue;

                if (QualitySettings.GetQualityLevel() != index)
                    QualitySettings.SetQualityLevel(index, true);

                Debug.Log($"[Quality] Tier {tier} -> quality level '{levelName}'");
                return;
            }

            var current = QualitySettings.GetQualityLevel();
            var currentName = current >= 0 && current < names.Length ? names[current] : "?";
            Debug.LogWarning($"[Quality] No quality level for tier {tier}, keeping '{currentName}'");
        }
    }
}
