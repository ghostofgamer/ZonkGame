using System;
using System.Threading;
using Base.Services.Analytics;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using UnityEngine;
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
        private bool _suspended;
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

        /// <summary>Подсказка видна или убрана: HUD прячет свою строку подсказки, чтобы они не спорили и не наезжали друг на друга.</summary>
        public event Action<bool> TipVisibleChanged;

        /// <summary>Спрятать подсказку на время окна поверх партии (пауза). Подсказка, открытая в это время, тоже ждёт.</summary>
        public void Suspend(bool suspended)
        {
            _suspended = suspended;
            if (_tip != null)
                _tip.gameObject.SetActive(!suspended);
        }

        /// <summary>Показать подсказку момента, если она есть и ещё не показывалась.</summary>
        public void Show(TutorialTrigger trigger)
        {
            if (TakeText(trigger, out var text))
                ShowTipAsync(text, null, null, null, false, default, 0f, null).Forget();
        }

        /// <summary>То же с кругом и стрелкой на элементе интерфейса.</summary>
        public void ShowPointing(TutorialTrigger trigger, RectTransform target, RectTransform second = null, object captionArg = null)
        {
            if (TakeText(trigger, out var text))
                ShowTipAsync(text, Caption(trigger, captionArg), target, second, false, default, 0f, null).Forget();
        }

        /// <summary>То же с кругом и стрелкой на точке стола, радиус в метрах.</summary>
        public void ShowPointing(TutorialTrigger trigger, Vector3 point, float radius, Camera camera)
        {
            if (TakeText(trigger, out var text))
                ShowTipAsync(text, Caption(trigger, null), null, null, true, point, radius, camera).Forget();
        }

        private string Caption(TutorialTrigger trigger, object arg)
        {
            var key = _config.Tutorial.Find(trigger).CaptionKey;
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            return arg != null ? _kit.T(key, arg) : _kit.T(key);
        }

        /// <summary>Подсказка момента будет показана: есть в конфиге и ещё не показывалась. Чтобы не считать цель стрелки зря.</summary>
        public bool ShouldShow(TutorialTrigger trigger)
        {
            var step = _config.Tutorial != null ? _config.Tutorial.Find(trigger) : null;
            return step != null && !string.IsNullOrEmpty(step.TextKey) && _saves.IsLoaded &&
                   (!Data.Seen.Contains(TriggerId(trigger)) || ReplayInEditor());
        }

        private bool TakeText(TutorialTrigger trigger, out string text)
        {
            text = null;
            if (!ShouldShow(trigger))
                return false;

            var id = TriggerId(trigger);
            var data = Data;
            if (!data.Seen.Contains(id))
            {
                data.Seen.Add(id);
                _saves.RequestSave();

                // Воронка обучения: tutorial → step → момент. По числу игроков на каждом шаге видно, где уходят новички.
                _analytics?.Track("tutorial", "step", id);
            }

            text = _kit.T(_config.Tutorial.Find(trigger).TextKey);
            return true;
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

        private async UniTaskVoid ShowTipAsync(string text, string caption, RectTransform target, RectTransform second, bool hasPoint, Vector3 point, float radius, Camera camera)
        {
            // Прежняя подсказка закрывается: видна только последняя.
            Hide();
            var cts = new CancellationTokenSource();
            _tipCts = cts;
            TipVisibleChanged?.Invoke(true);
            TutorialTipWindow tip = null;
            try
            {
                tip = await _ui.OpenAsync<TutorialTipWindow>(cts.Token, w =>
                {
                    w.Setup(text);
                    w.SetCaption(caption);
                    if (target != null)
                        w.PointAt(target, second);
                    else if (hasPoint)
                        w.PointAt(point, radius, camera);
                });
                _tip = tip;
                if (_suspended)
                    tip.gameObject.SetActive(false);
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
                {
                    _tipCts = null;
                    TipVisibleChanged?.Invoke(false);
                }
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
