using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Пакет монет за деньги: расходуемый товар площадки (ProductId как в консоли площадки). Цена в рублях
    /// задаётся на площадке, игра показывает её из каталога. Новый пакет = новый ассет, товар добавляется сам.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Coin Pack", fileName = "CoinPack")]
    public sealed class CoinPackConfig : ContentConfig
    {
        [Tooltip("ID расходуемого товара на площадке")]
        public string ProductId;

        public CurrencyConfig Currency;
        public int Amount = 1000;

        [Tooltip("Выгода относительно самого маленького пакета, % (только для подписи)")]
        public int BonusPercent;

        public int Order;
    }
}
