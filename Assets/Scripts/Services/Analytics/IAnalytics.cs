namespace Base.Services.Analytics
{
    /// <summary>
    /// Аналитика для кода игры: события раскладываются по «папкам».
    ///
    /// Как это выглядит в AppMetrica (отчёт «События» → событие → «Параметры событий»):
    /// имя события — верхняя папка (категория: campaign, ads, shop), каждый Path — вложенная папка,
    /// Param — строка внутри папки со значением. В отчёте дерево раскрывается по щелчку, у каждого узла
    /// видно, сколько раз и сколько игроков до него дошли.
    /// <code>
    /// analytics.Event("campaign").Path("win", "chapter_1", "semenych").Param("turns", 9).Send();
    /// // campaign
    /// //   └ win
    /// //       └ chapter_1
    /// //           └ semenych
    /// //               └ turns: 9
    /// analytics.Track("tutorial", "step", "ChooseDice");   // короткая форма: только папки, последняя — значение
    /// </code>
    /// Правила: имена латиницей в snake_case, в значениях ID контента, а не локализованные названия.
    /// Вложенность вместе с Param — не больше 5 уровней (ограничение AppMetrica), лишнее обрезается.
    /// События — редкие и значимые (не каждый кадр и не каждый бросок): пакеты уходят сами.
    /// </summary>
    public interface IAnalytics
    {
        /// <summary>Отправляются ли события куда-то (на площадке есть аналитика).</summary>
        bool IsAvailable { get; }

        /// <summary>Начать событие в папке folder (имя события в отчёте). Заполнить и вызвать Send.</summary>
        AnalyticsEvent Event(string folder);

        /// <summary>Событие из одних папок: folder → path[0] → … → path[n-1] (последнее — значение).</summary>
        void Track(string folder, params string[] path);

        /// <summary>Свойство игрока (срез для всех отчётов): «дошёл до главы», «платящий», «день с установки».</summary>
        void SetUserProperty(string key, string value);

        void SetUserProperty(string key, double value);

        /// <summary>Отправить накопленное сейчас (перед выходом из игры).</summary>
        void Flush();
    }
}
