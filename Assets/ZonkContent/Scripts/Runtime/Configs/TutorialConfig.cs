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
        /// <summary>Ход игрока после первого: кости можно бросить своей рукой, потряся стакан.</summary>
        ManualRoll,
        /// <summary>Первый вход в «Бесконечный забег».</summary>
        EndlessRun,
        /// <summary>Первый вход в башню.</summary>
        Tower,
        /// <summary>Игрок взял стакан в первый раз: потрясти и толкнуть.</summary>
        ManualRollShake,
    }

    [Serializable]
    public sealed class TutorialStep
    {
        public TutorialTrigger Trigger;

        [Tooltip("Ключ текста подсказки в Texts.csv")]
        public string TextKey;

        [Tooltip("Короткая надпись над стрелкой (подсказка с целью). Пусто — без надписи")]
        public string CaptionKey;
    }

    /// <summary>Заданный бросок партии обучения: грань на каждую кость в руке, по порядку.</summary>
    [Serializable]
    public sealed class ScriptedRoll
    {
        public int[] Faces;

        public ScriptedRoll(params int[] faces)
        {
            Faces = faces;
        }
    }

    /// <summary>
    /// Обучение: при первом запуске в меню только кнопка обучения, партия с заданными бросками (TutorialState).
    /// Шаги с целью идут только в ней, остальные подсказки — один раз, когда впервые случается их момент.
    /// Новая подсказка — строка в списке (и ключ текста), без кода.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Tutorial", fileName = "Tutorial")]
    public sealed class TutorialConfig : ScriptableObject
    {
        public List<TutorialStep> Steps = new List<TutorialStep>();

        [Tooltip("Радиус круга в середине лотка на шаге «потрясите и толкните», метры")]
        public float ShakeFocusRadius = 0.45f;

        [Header("Партия обучения")]
        [Tooltip("Соперник партии обучения")]
        public OpponentConfig Opponent;

        [Tooltip("Цель партии: короткая, чтобы закончить за 2–3 хода")]
        public int TargetScore = 1500;

        [Tooltip("Броски игрока по порядку. Когда кончатся — обычный ГСЧ")]
        public List<ScriptedRoll> PlayerRolls = new List<ScriptedRoll>
        {
            new ScriptedRoll(5, 5, 5, 2, 3, 4),
            new ScriptedRoll(1, 2, 6, 3, 4, 4),
            new ScriptedRoll(1, 1, 1, 5, 5, 2),
        };

        [Tooltip("Броски соперника по порядку: два Зонка, чтобы новичок увидел Зонк и выиграл")]
        public List<ScriptedRoll> OpponentRolls = new List<ScriptedRoll>
        {
            new ScriptedRoll(2, 3, 4, 6, 2, 3),
            new ScriptedRoll(3, 4, 6, 2, 6, 4),
        };

        public static IReadOnlyList<IReadOnlyList<int>> Faces(List<ScriptedRoll> rolls)
        {
            var result = new List<IReadOnlyList<int>>(rolls.Count);
            foreach (var roll in rolls)
                result.Add(roll.Faces);
            return result;
        }

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
