using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Валюта: монеты, энергия и любые будущие. Энергия — та же валюта с восстановлением по времени.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Currency", fileName = "Currency")]
    public sealed class CurrencyConfig : ContentConfig
    {
        public Sprite Icon;
        public Color Color = Color.white;

        [Tooltip("Сколько у нового игрока")]
        public int StartAmount;

        [Tooltip("Потолок, до которого идёт восстановление. 0 = без потолка и без восстановления")]
        public int RegenCap;

        [Tooltip("Сколько единиц восстанавливается за интервал")]
        public int RegenAmount = 1;

        [Tooltip("Интервал восстановления в секундах")]
        public int RegenIntervalSeconds = 600;

        public bool HasRegen => RegenCap > 0 && RegenAmount > 0 && RegenIntervalSeconds > 0;
    }
}
