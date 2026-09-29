using System;

namespace Zonk.Core.Progress
{
    /// <summary>
    /// Номера дней и недель для заданий. День меняется в местную полночь игрока, неделя — в полночь с воскресенья
    /// на понедельник. Номера считаются от 1 января 2001 года (понедельник), поэтому неделя = день / 7.
    /// </summary>
    public static class QuestCalendar
    {
        private static readonly DateTime Epoch = new DateTime(2001, 1, 1);

        public static int DayIndex(DateTime localNow)
        {
            return (int)Math.Floor((localNow.Date - Epoch).TotalDays);
        }

        public static int WeekIndex(DateTime localNow)
        {
            var day = DayIndex(localNow);
            return day >= 0 ? day / 7 : (day - 6) / 7;
        }

        /// <summary>Сколько осталось до новой порции дневных заданий.</summary>
        public static TimeSpan TimeToNextDay(DateTime localNow)
        {
            return localNow.Date.AddDays(1) - localNow;
        }

        /// <summary>Сколько осталось до новой недели (понедельник 00:00).</summary>
        public static TimeSpan TimeToNextWeek(DateTime localNow)
        {
            var nextWeekDay = (WeekIndex(localNow) + 1) * 7;
            return Epoch.AddDays(nextWeekDay) - localNow;
        }
    }
}
