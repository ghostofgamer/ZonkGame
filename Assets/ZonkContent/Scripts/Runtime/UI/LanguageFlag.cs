using System;
using UnityEngine;

namespace Zonk.UI
{
    /// <summary>Флаг языка (UiConfig.LanguageFlags): код ISO 639-1 и картинка.</summary>
    [Serializable]
    public sealed class LanguageFlag
    {
        [Tooltip("Код языка: ru, en, tr, es, pt, de, fr")]
        public string Code;

        public Sprite Flag;
    }
}
