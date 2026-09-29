using System;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Кнопка «награда за рекламу» в главном меню (GameConfig.MenuAdOffers): валюта, сколько за просмотр, лимит в день.
    /// Новая кнопка = новая строка в списке, без кода.
    /// </summary>
    [Serializable]
    public sealed class MenuAdOffer
    {
        [Tooltip("Постоянный ID: по нему считаются просмотры за день и место рекламы (menu_<Id>)")]
        public string Id;

        public CurrencyConfig Currency;
        public int Amount = 50;

        [Tooltip("Сколько раз в день. 0 = без лимита")]
        public int PerDay = 3;

        [Tooltip("Прятать кнопку, когда валюта на потолке восстановления (энергия полная): смотреть рекламу незачем")]
        public bool HideWhenFull;
    }
}
