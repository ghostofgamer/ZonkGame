using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Configs
{
    public enum QuestPeriod
    {
        Daily = 0,
        Weekly = 1,
    }

    /// <summary>
    /// Задание дня или недели. NameKey — текст задания: {0} — сколько нужно (Goal.Target), {1} — название из цели.
    /// Набор периода: сначала обязательные задания (Guaranteed), затем по весу из остальных, не больше одного из группы.
    /// Задание выдаётся, только если выполнены его условия и цель доступна игроку.
    /// Новое задание = новый ассет; новая цель, условие или событие = новый наследник QuestGoal / QuestCondition.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Quest", fileName = "Quest")]
    public sealed class QuestConfig : ContentConfig
    {
        public QuestPeriod Period;

        [Tooltip("Как часто задание выпадает относительно других заданий периода. 0 = не выдаётся")]
        public float Weight = 1f;

        [Tooltip("Выдаётся в каждом наборе периода (если выполнены условия), например «посмотреть рекламу». Не заменяется")]
        public bool Guaranteed;

        [Tooltip("Группа похожих заданий: в одном наборе не больше одного задания группы. Пусто = без группы")]
        public string Group;

        [Tooltip("Когда задание можно выдать: все условия должны выполняться")]
        [SerializeReference, SubclassSelector]
        public List<QuestCondition> Conditions = new List<QuestCondition>();

        [SerializeReference, SubclassSelector]
        public QuestGoal Goal;

        [SerializeReference, SubclassSelector]
        public List<Reward> Rewards = new List<Reward>();

        /// <summary>Можно ли выдать задание игроку сейчас: цель доступна и все условия выполнены.</summary>
        public bool IsAvailable(Zonk.Progress.QuestContext context)
        {
            if (Goal == null || !Goal.IsAvailable(context))
                return false;

            foreach (var condition in Conditions)
            {
                if (condition != null && !condition.IsMet(context))
                    return false;
            }

            return true;
        }

        /// <summary>Текст задания: {0} — сколько нужно, {1} — название из цели (например, кости).</summary>
        public string Describe(System.Func<string, string> localize)
        {
            var format = localize(NameKey);
            var extraKey = Goal != null ? Goal.TextArgKey : null;
            var extra = string.IsNullOrEmpty(extraKey) ? string.Empty : localize(extraKey);
            try
            {
                return string.Format(format, Goal != null ? Goal.Target : 1, extra);
            }
            catch (System.FormatException)
            {
                return format;
            }
        }
    }
}
