using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>Момент игры, когда может появиться подсказка обучения. Новый момент — значение и вызов в коде.</summary>
    public enum TutorialTrigger
    {
        /// <summary>Первое главное меню.</summary>
        MainMenu,
        /// <summary>Ход игрока, кости ещё не брошены.</summary>
        BeforeFirstRoll,
        /// <summary>Кости брошены, нужно выбрать приносящие очки.</summary>
        ChooseDice,
        /// <summary>Кости отложены: бросить остальные или забрать очки.</summary>
        AfterKeep,
        /// <summary>Зонк у игрока.</summary>
        Zonk,
        /// <summary>Игрок забрал очки.</summary>
        Bank,
        /// <summary>Горячие кости у игрока.</summary>
        HotDice,
        /// <summary>Первый ход соперника.</summary>
        OpponentTurn,
        /// <summary>Кто-то набрал цель: начался последний круг.</summary>
        FinalRound,
    }

    [Serializable]
    public sealed class TutorialStep
    {
        public TutorialTrigger Trigger;

        [Tooltip("Ключ текста подсказки в Texts.csv")]
        public string TextKey;
    }

    /// <summary>
    /// Обучение: подсказки поверх настоящей партии, каждая один раз, когда впервые случается её момент.
    /// Не останавливают игру. Новая подсказка — строка в списке (и ключ текста), без кода.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Tutorial", fileName = "Tutorial")]
    public sealed class TutorialConfig : ScriptableObject
    {
        public List<TutorialStep> Steps = new List<TutorialStep>();

        public TutorialStep Find(TutorialTrigger trigger)
        {
            foreach (var step in Steps)
            {
                if (step != null && step.Trigger == trigger)
                    return step;
            }

            return null;
        }
    }
}
