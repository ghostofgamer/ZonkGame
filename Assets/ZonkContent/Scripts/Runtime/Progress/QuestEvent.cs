using System.Collections.Generic;
using Zonk.Configs;
using Zonk.Core.Rules;

namespace Zonk.Progress
{
    public enum QuestEventKind
    {
        /// <summary>Партия закончилась: Won, Surrendered, VsBoss, Dice.</summary>
        MatchFinished,
        /// <summary>Игрок отложил кости: Score.</summary>
        Keep,
        /// <summary>Игрок забрал очки: Amount.</summary>
        Bank,
        HotDice,
        /// <summary>Первый заход в игру за день.</summary>
        DayVisited,
        /// <summary>Игрок забрал награду дневного задания.</summary>
        DailyQuestClaimed,
        /// <summary>Игрок получил награду за рекламу.</summary>
        AdWatched,
        /// <summary>Событие по тегу (Tag, Amount) для CustomEventGoal: новые события без нового кода заданий.</summary>
        Custom,
    }

    /// <summary>Событие игры для заданий. Сообщают MatchRunner (ход), состояния режимов (итог партии) и сами задания.</summary>
    public sealed class QuestEvent
    {
        public QuestEventKind Kind;

        /// <summary>Партия вдвоём на одном экране: цели по умолчанию её не считают.</summary>
        public bool HotSeat;

        public bool Won;
        public bool Surrendered;
        public bool VsBoss;
        public int Amount;
        public ScoreResult Score;

        /// <summary>Кости игрока в партии.</summary>
        public IReadOnlyList<DieConfig> Dice;

        /// <summary>Тег события Custom.</summary>
        public string Tag;

        public static QuestEvent CustomEvent(string tag, int amount = 1)
        {
            return new QuestEvent { Kind = QuestEventKind.Custom, Tag = tag, Amount = amount };
        }
    }
}
