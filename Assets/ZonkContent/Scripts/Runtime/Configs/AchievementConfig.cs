using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Достижение: цель (AchievementGoal — что считать) и порог Target, награды, редкость (цвет). Название —
    /// NameKey и римская цифра ступени Tier («Победитель III»), описание — DescriptionKey с {0} = Target.
    /// Ступени одной цели — отдельные ассеты (ach_wins_10, ach_wins_100…). Открытое достижение выдаёт награды сразу.
    /// Новое достижение — ассет; новый вид цели — наследник AchievementGoal.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Achievement", fileName = "Achievement")]
    public sealed class AchievementConfig : ContentConfig
    {
        [Tooltip("Ключ описания, {0} — порог")]
        public string DescriptionKey;

        [Tooltip("Ступень для названия (римская цифра); 0 — без цифры")]
        [Min(0)] public int Tier;

        [Tooltip("Порядок в списке")]
        public int Order;

        public Rarity Rarity;

        [SerializeReference, SubclassSelector]
        public AchievementGoal Goal;

        [Min(1)] public long Target = 1;

        [Tooltip("Очков талантов за открытие (трудные достижения: редкие 1, легендарные 2–3)")]
        [Min(0)] public int TalentPoints;

        [SerializeReference, SubclassSelector]
        public List<Reward> Rewards = new List<Reward>();
    }
}
