using System;
using UnityEngine;
using Zonk.Core.Rules;
using Zonk.Progress;

namespace Zonk.Configs
{
    /// <summary>
    /// Цель задания: сколько нужно (Target) и сколько даёт событие игры. Новая цель = новый наследник,
    /// в ассете задания он появится в списке выбора. Партии вдвоём по умолчанию не считаются: иначе задания
    /// выполняются игрой с самим собой.
    /// </summary>
    [Serializable]
    public abstract class QuestGoal
    {
        [Min(1)] public int Target = 1;

        [Tooltip("Считать партии вдвоём на одном экране")]
        public bool CountHotSeat;

        /// <summary>Можно ли выдать задание этому игроку (например, нужная кость уже открыта).</summary>
        public virtual bool IsAvailable(QuestContext context)
        {
            return true;
        }

        /// <summary>Ключ текста для {1} в тексте задания (название кости и т.п.). null — нет.</summary>
        public virtual string TextArgKey => null;

        /// <summary>У строки задания кнопка «Смотреть рекламу»: задание выполняется прямо из окна.</summary>
        public virtual bool OffersAdButton => false;

        /// <summary>На сколько продвинуть задание. Событие уже отфильтровано по партиям вдвоём.</summary>
        public abstract int Progress(QuestEvent e);
    }

    /// <summary>Сыграть партии до конца (сдача не считается).</summary>
    [Serializable]
    public sealed class PlayMatchesGoal : QuestGoal
    {
        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.MatchFinished && !e.Surrendered ? 1 : 0;
        }
    }

    /// <summary>Выиграть партии; можно только у боссов.</summary>
    [Serializable]
    public sealed class WinMatchesGoal : QuestGoal
    {
        public bool BossOnly;

        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.MatchFinished && e.Won && (!BossOnly || e.VsBoss) ? 1 : 0;
        }
    }

    /// <summary>Выиграть с определённой особой костью в наборе. Выдаётся, только если кость открыта.</summary>
    [Serializable]
    public sealed class WinWithDieGoal : QuestGoal
    {
        public DieConfig Die;

        public override string TextArgKey => Die != null ? Die.NameKey : null;

        public override bool IsAvailable(QuestContext context)
        {
            return Die != null && context.Inventory != null && context.Inventory.IsOwned(Die);
        }

        public override int Progress(QuestEvent e)
        {
            if (e.Kind != QuestEventKind.MatchFinished || !e.Won || e.Dice == null)
                return 0;

            foreach (var die in e.Dice)
            {
                if (die == Die)
                    return 1;
            }

            return 0;
        }
    }

    /// <summary>Выиграть только обычными костями.</summary>
    [Serializable]
    public sealed class WinWithoutSpecialDiceGoal : QuestGoal
    {
        public override int Progress(QuestEvent e)
        {
            if (e.Kind != QuestEventKind.MatchFinished || !e.Won || e.Dice == null)
                return 0;

            foreach (var die in e.Dice)
            {
                if (die != null && die.IsSpecial)
                    return 0;
            }

            return 1;
        }
    }

    /// <summary>Забрать в сумме столько очков.</summary>
    [Serializable]
    public sealed class BankPointsGoal : QuestGoal
    {
        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.Bank ? e.Amount : 0;
        }
    }

    /// <summary>Забрать за один ход не меньше Points (Target — сколько раз).</summary>
    [Serializable]
    public sealed class BigTurnGoal : QuestGoal
    {
        public int Points = 1500;

        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.Bank && e.Amount >= Points ? 1 : 0;
        }
    }

    /// <summary>Получить горячие кости.</summary>
    [Serializable]
    public sealed class HotDiceGoal : QuestGoal
    {
        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.HotDice ? 1 : 0;
        }
    }

    /// <summary>Отложить комбинации вида Category (стрит, три и больше одинаковых…).</summary>
    [Serializable]
    public sealed class ComboGoal : QuestGoal
    {
        [Tooltip("Категория комбинации: single, of_a_kind, straight, three_pairs (ComboCategory)")]
        public string Category = ComboCategory.Straight;

        public override int Progress(QuestEvent e)
        {
            if (e.Kind != QuestEventKind.Keep || e.Score == null || e.Score.Combos == null)
                return 0;

            var count = 0;
            foreach (var combo in e.Score.Combos)
            {
                if (combo.Category == Category)
                    count++;
            }

            return count;
        }
    }

    /// <summary>Заходить в игру в разные дни (недельное задание).</summary>
    [Serializable]
    public sealed class VisitDaysGoal : QuestGoal
    {
        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.DayVisited ? 1 : 0;
        }
    }

    /// <summary>Выполнить и забрать дневные задания (недельное задание).</summary>
    [Serializable]
    public sealed class ClaimDailyQuestsGoal : QuestGoal
    {
        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.DailyQuestClaimed ? 1 : 0;
        }
    }

    /// <summary>
    /// Посмотреть рекламу за награду (любую: удвоение, энергия, магазин или кнопка в окне заданий).
    /// Выдаётся всегда: готовность рекламы меняется от минуты к минуте (на VK — «реклама предзагружена»),
    /// поэтому кнопка «Смотреть» просто ждёт, пока реклама появится.
    /// </summary>
    [Serializable]
    public sealed class WatchAdsGoal : QuestGoal
    {
        public override bool OffersAdButton => true;

        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.AdWatched ? 1 : 0;
        }
    }

    /// <summary>
    /// Любое событие игры по тегу: код сообщает IQuestService.Report(QuestEvent.CustomEvent("тег", сколько)),
    /// задание на него делается ассетом без нового класса цели. Теги сообщаемых событий — в README.
    /// </summary>
    [Serializable]
    public sealed class CustomEventGoal : QuestGoal
    {
        public string Tag;

        public override int Progress(QuestEvent e)
        {
            return e.Kind == QuestEventKind.Custom && e.Tag == Tag ? Math.Max(1, e.Amount) : 0;
        }
    }
}
