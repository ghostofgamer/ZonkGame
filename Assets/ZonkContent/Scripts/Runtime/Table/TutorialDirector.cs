using System;
using System.Threading;
using Base.Services.Analytics;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.Table
{
    /// <summary>
    /// Обучение в сцене стола: когда впервые случается момент игры (TutorialTrigger), показывает его подсказку
    /// из GameConfig.Tutorial и запоминает её в сохранении. Одновременно видна одна подсказка: новая заменяет старую.
    /// Игра при этом не ждёт. В редакторе обучение можно проиграть заново: меню Zonk/Debug/Replay Tutorial.
    /// </summary>
    public sealed class TutorialDirector : IDisposable
    {
        private readonly IUiService _ui;
        private readonly GameConfig _config;
        private readonly ISaveStore _saves;
        private readonly UiKit _kit;
        private readonly IAnalytics _analytics;
        private TutorialTipWindow _tip;
        private CancellationTokenSource _tipCts;

        public TutorialDirector(IUiService ui, GameConfig config, ISaveStore saves, UiKit kit, IAnalytics analytics)
        {
            _ui = ui;
            _config = config;
            _saves = saves;
            _kit = kit;
            _analytics = analytics;
        }

#if UNITY_EDITOR
        public const string ReplayEditorKey = "Zonk.ReplayTutorial";
#endif

        private TutorialSave Data => _saves.Get<TutorialSave>(SaveKeys.Tutorial);

        /// <summary>Показать подсказку момента, если она есть и ещё не показывалась.</summary>
        public void Show(TutorialTrigger trigger)
        {
            var step = _config.Tutorial != null ? _config.Tutorial.Find(trigger) : null;
            if (step == null || string.IsNullOrEmpty(step.TextKey) || !_saves.IsLoaded)
                return;

            var id = TriggerId(trigger);
            var data = Data;
            if (data.Seen.Contains(id) && !ReplayInEditor())
                return;

            if (!data.Seen.Contains(id))
            {
                data.Seen.Add(id);
                _saves.RequestSave();

                // Воронка обучения: tutorial → step → момент. По числу игроков на каждом шаге видно, где уходят новички.
                _analytics?.Track("tutorial", "step", id);
            }

            ShowTipAsync(_kit.T(step.TextKey)).Forget();
        }

        /// <summary>Подсказка момента уже показывалась игроку.</summary>
        public bool WasSeen(TutorialTrigger trigger)
        {
            return _saves.IsLoaded && Data.Seen.Contains(TriggerId(trigger));
        }

        /// <summary>Убрать подсказку (конец партии, уход из меню).</summary>
        public void Hide()
        {
            _tipCts?.Cancel();
        }

        public void Dispose()
        {
            Hide();
        }

        private async UniTaskVoid ShowTipAsync(string text)
        {
            // Прежняя подсказка закрывается: видна только последняя.
            Hide();
            var cts = new CancellationTokenSource();
            _tipCts = cts;
            TutorialTipWindow tip = null;
            try
            {
                tip = await _ui.OpenAsync<TutorialTipWindow>(cts.Token, w => w.Setup(text));
                _tip = tip;
                await tip.WaitCloseRequestAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (tip != null)
                    _ui.CloseAsync(tip, CancellationToken.None).Forget();
                if (_tip == tip)
                    _tip = null;
                if (_tipCts == cts)
                    _tipCts = null;
                cts.Dispose();
            }
        }

        // Имена моментов (ID в сохранении): Show зовут на каждом ходу партии, строка создаётся один раз на момент.
        private static readonly string[] TriggerIds = new string[Enum.GetValues(typeof(TutorialTrigger)).Length];

        private static string TriggerId(TutorialTrigger trigger)
        {
            var index = (int)trigger;
            if (index < 0 || index >= TriggerIds.Length)
                return trigger.ToString();
            return TriggerIds[index] ?? (TriggerIds[index] = trigger.ToString());
        }

        private static bool ReplayInEditor()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(ReplayEditorKey, false);
#else
            return false;
#endif
        }
    }
}
