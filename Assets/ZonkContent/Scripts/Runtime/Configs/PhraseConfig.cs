using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>Быстрая фраза в партии. Сейчас видна локально и вызывает реакцию ИИ, позже уйдёт по сети.</summary>
    [CreateAssetMenu(menuName = "Zonk/Phrase", fileName = "Phrase")]
    public sealed class PhraseConfig : ContentConfig
    {
        public int Order;
        public AvatarGesture Gesture;
    }
}
