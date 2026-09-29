using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Уровень мастерства особой кости (GameConfig.MasteryLevels, по возрастанию очков). Очки — забранные очки
    /// комбинаций, в которые входила кость, в партиях против соперников. Вид кости на уровне задаёт
    /// DieConfig.MasteryLooks[уровень - 1], свечение метки — Glow.
    /// </summary>
    [Serializable]
    public sealed class MasteryLevel
    {
        [Tooltip("Ключ названия уровня: «Бронза», «Серебро»…")]
        public string NameKey;

        [Tooltip("Сколько очков нужно набрать костью для уровня")]
        public int Points = 1500;

        [Tooltip("Цвет уровня: звёзды в интерфейсе и точки на текстурах мастерства")]
        public Color Color = Color.white;

        [Tooltip("Сила свечения метки кости на этом уровне (1 = как без мастерства)")]
        public float Glow = 1f;

        [SerializeReference, SubclassSelector]
        public List<Reward> Rewards = new List<Reward>();
    }
}
