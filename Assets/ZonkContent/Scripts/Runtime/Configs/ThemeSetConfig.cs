using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>
    /// Набор («Пиратский корабль», стартовый набор, «Без рекламы»): предметы, кости и разовые награды, продаётся целиком.
    /// Купленный за деньги набор — право площадки: его предметы открыты и на новом устройстве (Inventory).
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Theme Set", fileName = "Theme")]
    public sealed class ThemeSetConfig : ContentConfig
    {
        public Sprite Icon;
        public List<CosmeticItemConfig> Items = new List<CosmeticItemConfig>();

        [Tooltip("Особые кости в наборе (например, «все кости» за покупку)")]
        public List<DieConfig> Dice = new List<DieConfig>();

        [Tooltip("Разовые награды при получении набора: монеты, энергия. Выдаются один раз")]
        [SerializeReference, SubclassSelector]
        public List<Reward> Rewards = new List<Reward>();

        [Tooltip("Ключ описания в магазине: что внутри и чем выгодно")]
        public string DescriptionKey;

        [Tooltip("Показывать кнопкой в главном меню, пока не куплен (стартовый набор)")]
        public bool Featured;

        [Tooltip("Порядок во вкладке наборов")]
        public int Order;
        public Price Price = new Price();
    }
}
