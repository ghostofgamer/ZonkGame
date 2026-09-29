using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>Набор предметов разных слотов («Пиратский корабль»). Продаётся целиком, обычно со скидкой.</summary>
    [CreateAssetMenu(menuName = "Zonk/Theme Set", fileName = "Theme")]
    public sealed class ThemeSetConfig : ContentConfig
    {
        public Sprite Icon;
        public List<CosmeticItemConfig> Items = new List<CosmeticItemConfig>();
        public Price Price = new Price();
    }
}
