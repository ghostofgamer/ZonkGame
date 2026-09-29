using System;
using Zonk.Progress;

namespace Zonk.Configs
{
    /// <summary>
    /// Условие выдачи задания: задание попадает в набор дня (недели), только если все его условия выполнены.
    /// Новое условие = новый наследник, в ассете задания он появится в списке выбора.
    /// </summary>
    [Serializable]
    public abstract class QuestCondition
    {
        public abstract bool IsMet(QuestContext context);
    }

    /// <summary>Награду за рекламу можно получить в момент выдачи заданий (меняется со временем, см. QuestContext).</summary>
    [Serializable]
    public sealed class AdsAvailableCondition : QuestCondition
    {
        public override bool IsMet(QuestContext context)
        {
            return context.AdsAvailable;
        }
    }

    /// <summary>У игрока есть предмет: кость, скин, стакан, локация.</summary>
    [Serializable]
    public sealed class OwnsContentCondition : QuestCondition
    {
        public ContentConfig Item;

        public override bool IsMet(QuestContext context)
        {
            return Item != null && context.Inventory != null && context.Inventory.IsOwned(Item);
        }
    }

    /// <summary>Глава кампании открыта: задания про боссов и поздние локации не выпадают новичку.</summary>
    [Serializable]
    public sealed class ChapterUnlockedCondition : QuestCondition
    {
        public ChapterConfig Chapter;

        public override bool IsMet(QuestContext context)
        {
            return Chapter != null && context.Campaign != null && context.Campaign.IsChapterUnlocked(Chapter);
        }
    }

    /// <summary>Соперник уже побеждён хотя бы раз.</summary>
    [Serializable]
    public sealed class OpponentBeatenCondition : QuestCondition
    {
        public OpponentConfig Opponent;

        public override bool IsMet(QuestContext context)
        {
            if (Opponent == null || context.Campaign == null)
                return false;

            foreach (var chapter in context.Campaign.Chapters)
            {
                if (chapter != null && chapter.Opponents.Contains(Opponent))
                    return context.Campaign.GetState(chapter, Opponent) == OpponentState.Beaten;
            }

            return false;
        }
    }
}
