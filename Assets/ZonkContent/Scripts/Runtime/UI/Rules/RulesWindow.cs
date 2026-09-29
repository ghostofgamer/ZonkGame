using System.Threading;
using Base.Core.Localization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Zonk.Configs;
using Zonk.Core.Rules;
using Zonk.UI.Transitions;

namespace Zonk.UI.Rules
{
    /// <summary>
    /// Книга правил: слева все комбинации с картинками костей и очками по текущим правилам,
    /// справа страницы правил с листанием. Смена страницы анимируется стилями PageOut/PageIn из префаба.
    /// Содержимое — RulesBookConfig, тексты — ключи локализации; при смене языка всё перерисовывается.
    /// </summary>
    public sealed class RulesWindow : UiWindow
    {
        [Header("Тексты")]
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _combosTitle;
        [SerializeField] private TMP_Text _pageTitle;
        [SerializeField] private TMP_Text _pageText;
        [SerializeField] private TMP_Text _pageCounter;
        [SerializeField] private Image _illustration;

        [Header("Комбинации")]
        [SerializeField] private RectTransform _comboList;
        [SerializeField] private RulesComboRow _rowTemplate;

        [Header("Листание")]
        [SerializeField] private RectTransform _pageContent;
        [SerializeField] private Button _previous;
        [SerializeField] private Button _next;
        [SerializeField] private Button _close;
        [SerializeField] private UiTransitionConfig _pageOut;
        [SerializeField] private UiTransitionConfig _pageIn;

        private ILocalization _localization;
        private UiConfig _ui;
        private RulesBookConfig _book;
        private UiTransitionTarget _pageTarget;
        private int _page;
        private bool _turning;

        [Inject]
        public void Construct(ILocalization localization, GameConfig config)
        {
            _localization = localization;
            _ui = config != null ? config.Ui : null;
            _book = _ui != null ? _ui.RulesBook : null;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, TMP_Text combosTitle, TMP_Text pageTitle, TMP_Text pageText, TMP_Text pageCounter,
            Image illustration, RectTransform comboList, RulesComboRow rowTemplate, RectTransform pageContent, Button previous,
            Button next, Button close, UiTransitionConfig pageOut, UiTransitionConfig pageIn)
        {
            _title = title;
            _combosTitle = combosTitle;
            _pageTitle = pageTitle;
            _pageText = pageText;
            _pageCounter = pageCounter;
            _illustration = illustration;
            _comboList = comboList;
            _rowTemplate = rowTemplate;
            _pageContent = pageContent;
            _previous = previous;
            _next = next;
            _close = close;
            _pageOut = pageOut;
            _pageIn = pageIn;
        }
#endif

        private void Awake()
        {
            _previous.onClick.AddListener(() => TurnAsync(-1).Forget());
            _next.onClick.AddListener(() => TurnAsync(1).Forget());
            _close.onClick.AddListener(RequestClose);
            _rowTemplate.gameObject.SetActive(false);
        }

        protected override void OnShowing()
        {
            if (_localization != null)
                _localization.LanguageChanged += Refresh;

            _page = 0;
            Refresh();
        }

        protected override void OnHiding()
        {
            if (_localization != null)
                _localization.LanguageChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (_localization != null)
                _localization.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            _title.text = T("rules.title");
            SetButtonText(_previous, T("rules.prev"));
            SetButtonText(_next, T("rules.next"));
            _combosTitle.text = T("rules.combos");
            BuildCombos();
            ShowPage();
        }

        private void BuildCombos()
        {
            foreach (Transform child in _comboList)
            {
                if (child != _rowTemplate.transform)
                    Destroy(child.gameObject);
            }

            if (_book == null)
                return;

            var rules = _book.Rules != null ? _book.Rules.ToRuleSet() : RuleSet.CreateClassic();
            var calculator = new ScoreCalculator(rules.Rules);

            foreach (var combo in _book.Combos)
            {
                if (combo == null || combo.Faces == null || combo.Faces.Length == 0)
                    continue;

                // Комбинация, которой нет в правилах, в книгу не попадает.
                var score = calculator.EvaluateFaces(combo.Faces);
                if (!score.IsValid)
                    continue;

                var row = Instantiate(_rowTemplate, _comboList);
                row.gameObject.SetActive(true);
                row.Set(combo.Faces, _ui.DieFaces, T(combo.NameKey), T("rules.points", score.Score));
            }
        }

        private async UniTaskVoid TurnAsync(int direction)
        {
            if (_turning || _book == null || _book.Pages.Count == 0)
                return;

            var target = Mathf.Clamp(_page + direction, 0, _book.Pages.Count - 1);
            if (target == _page)
                return;

            _turning = true;
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                _pageTarget = _pageTarget ?? new UiTransitionTarget(_pageContent, PageGroup());
                if (_pageOut != null)
                    await _pageOut.PlayAsync(_pageTarget, ct);

                _page = target;
                ShowPage();

                if (_pageIn != null)
                    await _pageIn.PlayAsync(_pageTarget, ct);
                else
                    _pageTarget.Reset();
            }
            finally
            {
                _turning = false;
            }
        }

        private void ShowPage()
        {
            var count = _book != null ? _book.Pages.Count : 0;
            if (count == 0)
            {
                _pageTitle.text = string.Empty;
                _pageText.text = string.Empty;
                _pageCounter.text = string.Empty;
                return;
            }

            _page = Mathf.Clamp(_page, 0, count - 1);
            var page = _book.Pages[_page];
            _pageTitle.text = T(page.TitleKey);
            _pageText.text = T(page.TextKey);
            _pageCounter.text = T("rules.pageCounter", _page + 1, count);

            if (_illustration != null)
            {
                _illustration.sprite = page.Illustration;
                _illustration.gameObject.SetActive(page.Illustration != null);
            }

            _previous.interactable = _page > 0;
            _next.interactable = _page < count - 1;
        }

        private static void SetButtonText(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = text;
        }

        private CanvasGroup PageGroup()
        {
            return _pageContent.TryGetComponent<CanvasGroup>(out var group) ? group : _pageContent.gameObject.AddComponent<CanvasGroup>();
        }

        private string T(string key)
        {
            return _localization != null ? _localization.Get(key) : key;
        }

        private string T(string key, params object[] args)
        {
            var format = T(key);
            try
            {
                return string.Format(format, args);
            }
            catch (System.FormatException)
            {
                return format;
            }
        }
    }
}
