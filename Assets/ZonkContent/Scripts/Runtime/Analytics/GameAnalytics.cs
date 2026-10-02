using System;
using System.Threading;
using Base.Core.Localization;
using Base.Platform;
using Base.Services.Analytics;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.Analytics
{
    /// <summary>
    /// Все события игры для аналитики — здесь, по папкам. В AppMetrica: «События» → папка (имя события) →
    /// «Параметры событий» → дерево. Имена латиницей, snake_case; значения — ID и коды, не тексты на языке игрока.
    ///
    /// Папки (новые события добавлять по этой схеме):
    /// <code>
    /// test                          тестовые события (AnalyticsConfig.SendTestEvents), перед выпуском выключить
    ///   └ game_start                игра запустилась
    ///       ├ platform: rustore
    ///       └ language: ru
    /// session                       заходы игроков
    ///   └ start
    ///       └ first | return        первый запуск или повторный
    ///           ├ platform: rustore
    ///           └ language: ru
    /// progress                      прогресс игрока
    ///   └ player_level
    ///       └ level: 7                новый уровень игрока (сколько игроков дошло до каждого)
    /// ads                           (дальше) реклама
    ///   ├ rewarded
    ///   │   └ request | shown | completed | closed | failed | no_ad
    ///   │       └ placement: double_reward | energy | shop | menu | quest
    ///   └ interstitial
    ///       └ request | shown | skipped | failed
    ///           └ trigger: match_end | restart
    /// </code>
    /// Сколько игроков зашло: папка session → start, в отчёте у события есть число пользователей;
    /// first — новые игроки, return — вернувшиеся.
    /// </summary>
    public sealed class GameAnalytics : IInitializable, IDisposable
    {
        public const string TestFolder = "test";
        public const string SessionFolder = "session";
        public const string AdsFolder = "ads";
        public const string ProgressFolder = "progress";

        private readonly IAnalytics _analytics;
        private readonly ISaveStore _saves;
        private readonly IPlatformService _platform;
        private readonly ILocalization _localization;
        private readonly AnalyticsConfig _config;
        private readonly IPlayerLevel _level;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public GameAnalytics(IAnalytics analytics, ISaveStore saves, IPlatformService platform, ILocalization localization,
            GameConfig config, [InjectOptional] IPlayerLevel level)
        {
            _level = level;
            _analytics = analytics;
            _saves = saves;
            _platform = platform;
            _localization = localization;
            _config = config != null && config.Analytics != null ? config.Analytics : AnalyticsConfig.Fallback;
        }

        public void Initialize()
        {
            if (_level != null)
                _level.LevelReached += OnLevelReached;
            SendGameStartAsync(_cts.Token).Forget();
        }

        public void Dispose()
        {
            if (_level != null)
                _level.LevelReached -= OnLevelReached;
            _cts.Cancel();
            _cts.Dispose();
        }

        private void OnLevelReached(int level)
        {
            _analytics.Event(ProgressFolder).Path("player_level").Param("level", level).Send();
        }

        private string Platform => _platform != null ? _platform.Platform.ToString().ToLowerInvariant() : "unknown";
        private string Language => _localization != null ? _localization.Language : "unknown";

        /// <summary>
        /// Старт игры — один раз за запуск, когда загрузилось сохранение (в нём счётчик запусков: первый вход или
        /// повторный). Тестовая метрика — в папку test, обычная — в session.
        /// </summary>
        private async UniTaskVoid SendGameStartAsync(CancellationToken ct)
        {
            if (await _saves.WaitLoadedAsync(ct).SuppressCancellationThrow())
                return;

            var data = _saves.Get<AnalyticsSave>(SaveKeys.Analytics);
            var first = data.Launches == 0;
            data.Launches++;
            _saves.RequestSave();

            if (_config.SendTestEvents)
            {
                _analytics.Event(TestFolder)
                    .Path("game_start")
                    .Param("platform", Platform)
                    .Param("language", Language)
                    .Send();
            }

            _analytics.Event(SessionFolder)
                .Path("start", first ? "first" : "return")
                .Param("platform", Platform)
                .Param("language", Language)
                .Send();
        }
    }
}
