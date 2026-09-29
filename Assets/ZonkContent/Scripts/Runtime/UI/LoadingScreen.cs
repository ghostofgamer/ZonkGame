using System;
using System.Threading;
using Base.Core.Localization;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;

namespace Zonk.UI
{
    /// <summary>
    /// Экран загрузки: полоса прогресса и случайная короткая подсказка, которая сменяется по таймеру.
    /// Живёт в сцене Bootstrap и переживает загрузку стола (DontDestroyOnLoad): стол закрывает его сам,
    /// когда меню готово (LoadingScreenHolder.HideAsync). Язык подсказки обновляется при смене языка.
    /// </summary>
    public sealed class LoadingScreen : UiWindow
    {
        [SerializeField] private RectTransform _fill;
        [SerializeField] private TMP_Text _tip;
        [SerializeField] private TMP_Text _title;

        private ILocalization _localization;
        private UiConfig _config;
        private LoadingScreenHolder _holder;
        private Tween _fillTween;
        private int _tipIndex = -1;
        private CancellationTokenSource _tipsCts;

        [Inject]
        public void Construct(ILocalization localization, GameConfig config, LoadingScreenHolder holder)
        {
            _localization = localization;
            _config = config != null ? config.Ui : null;
            _holder = holder;
        }

#if UNITY_EDITOR
        public void EditorSetup(RectTransform fill, TMP_Text tip, TMP_Text title)
        {
            _fill = fill;
            _tip = tip;
            _title = title;
        }
#endif

        private void Awake()
        {
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private void Start()
        {
            _holder?.Register(this);
            if (_localization != null)
                _localization.LanguageChanged += OnLanguageChanged;

            SetProgress(0.05f, true);
            RefreshTexts();
            _tipsCts = new CancellationTokenSource();
            RotateTipsAsync(_tipsCts.Token).Forget();
        }

        private void OnDestroy()
        {
            if (_localization != null)
                _localization.LanguageChanged -= OnLanguageChanged;
            _tipsCts?.Cancel();
            _tipsCts?.Dispose();
            _fillTween?.Kill();
        }

        /// <summary>Прогресс 0..1. Полоса догоняет значение плавно и никогда не едет назад.</summary>
        public void SetProgress(float value, bool instant = false)
        {
            if (_fill == null)
                return;

            value = Mathf.Clamp01(value);
            var current = _fill.anchorMax.x;
            if (value < current)
                return;

            _fillTween?.Kill();
            if (instant)
            {
                _fill.anchorMax = new Vector2(value, 1f);
                return;
            }

            _fillTween = DOTween.To(() => _fill.anchorMax.x, x => _fill.anchorMax = new Vector2(x, 1f), value, 0.35f)
                .SetUpdate(true).SetLink(gameObject);
        }

        private void OnLanguageChanged()
        {
            RefreshTexts();
        }

        private void RefreshTexts()
        {
            if (_localization == null)
                return;

            if (_title != null)
                _title.text = _localization.Get("game.title");
            ShowTip(_tipIndex < 0 ? PickTip() : _tipIndex);
        }

        private async UniTaskVoid RotateTipsAsync(CancellationToken ct)
        {
            var interval = _config != null ? Mathf.Max(1f, _config.TipInterval) : 4f;
            while (!ct.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(interval), true, cancellationToken: ct).SuppressCancellationThrow())
                    return;

                ShowTip(PickTip());
                if (_tip != null)
                {
                    _tip.alpha = 0f;
                    DOTween.To(() => _tip.alpha, a => _tip.alpha = a, 1f, 0.4f).SetUpdate(true).SetLink(gameObject);
                }
            }
        }

        private int PickTip()
        {
            var count = _config != null ? _config.TipKeys.Count : 0;
            if (count == 0)
                return -1;
            if (count == 1)
                return 0;

            int index;
            do
            {
                index = UnityEngine.Random.Range(0, count);
            } while (index == _tipIndex);

            return index;
        }

        private void ShowTip(int index)
        {
            _tipIndex = index;
            if (_tip == null || _localization == null || _config == null || index < 0 || index >= _config.TipKeys.Count)
                return;

            _tip.text = _localization.Get(_config.TipKeys[index]);
        }
    }

    /// <summary>
    /// Держит экран загрузки между сценами. Bootstrap регистрирует его, стол прячет и удаляет,
    /// когда первый экран готов. Живёт в ProjectContext.
    /// </summary>
    public sealed class LoadingScreenHolder
    {
        private LoadingScreen _screen;

        public void Register(LoadingScreen screen)
        {
            _screen = screen;
        }

        public void SetProgress(float value)
        {
            if (_screen != null)
                _screen.SetProgress(value);
        }

        public async UniTask HideAsync(CancellationToken ct)
        {
            if (_screen == null)
                return;

            var screen = _screen;
            _screen = null;
            screen.SetProgress(1f);
            await screen.HideAsync(ct);
            if (screen != null)
                UnityEngine.Object.Destroy(screen.transform.root.gameObject);
        }
    }
}
