using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Match;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    public enum ResultsChoice
    {
        Again,
        Menu,
        DoubleReward,
    }

    /// <summary>
    /// Итоги партии: победитель, строка на каждого игрока, награды, «Ещё раз», «В меню», удвоение за рекламу.
    /// Строки игроков и наград — копии шаблонов-текстов из префаба.
    /// </summary>
    public sealed class ResultsWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _lines;
        [SerializeField] private TMP_Text _lineTemplate;
        [SerializeField] private RectTransform _rewards;
        [SerializeField] private TMP_Text _rewardTemplate;

        [Tooltip("Звёзды за соперника (вложенный префаб Parts/Stars). Пусто — звёзды строкой текста")]
        [SerializeField] private StarsView _stars;

        [Tooltip("Уровень игрока: опыт за партию (вложенный префаб Parts/PlayerLevel). Пусто — опыт строкой текста")]
        [SerializeField] private PlayerLevelView _playerLevel;

        [SerializeField] private UiButtonView _menu;
        [SerializeField] private UiButtonView _again;
        [SerializeField] private UiButtonView _double;

        [Header("Прогрессия (детали Parts; в старом префабе пусто — итоги без них)")]
        [Tooltip("Плашка-взрыв поверх итогов: победа, новый уровень")]
        [SerializeField] private ComicBurstView _burst;

        [Tooltip("Крупный счёт партии")]
        [SerializeField] private TMP_Text _headline;

        [Tooltip("Блок «Дальше»: ради чего играть следующую партию")]
        [SerializeField] private UiLabelPlate _nextGoal;

        private UiConfig _uiConfig;
        private readonly Choice<ResultsChoice> _choice = new Choice<ResultsChoice>();

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform lines, TMP_Text lineTemplate, RectTransform rewards,
            TMP_Text rewardTemplate, UiButtonView menu, UiButtonView again, UiButtonView doubleReward, StarsView stars, PlayerLevelView playerLevel = null)
        {
            _playerLevel = playerLevel;
            _stars = stars;
            _title = title;
            _lines = lines;
            _lineTemplate = lineTemplate;
            _rewards = rewards;
            _rewardTemplate = rewardTemplate;
            _menu = menu;
            _again = again;
            _double = doubleReward;
        }

        public void EditorSetupProgress(ComicBurstView burst, TMP_Text headline, UiLabelPlate nextGoal)
        {
            _burst = burst;
            _headline = headline;
            _nextGoal = nextGoal;
        }
#endif

        [Zenject.Inject]
        public void Construct(GameConfig config)
        {
            // Строки итогов могут содержать звёзды.
            _uiConfig = config != null ? config.Ui : null;
            StarsText.Prepare(_lineTemplate, _uiConfig);
        }

        private void Awake()
        {
            _lineTemplate.gameObject.SetActive(false);
            _rewardTemplate.gameObject.SetActive(false);
            if (_burst != null)
                _burst.gameObject.SetActive(false);
            if (_nextGoal != null)
                _nextGoal.gameObject.SetActive(false);
            if (_stars != null)
                _stars.gameObject.SetActive(false);
            if (_playerLevel != null)
                _playerLevel.gameObject.SetActive(false);
            _menu.OnClick(() => _choice.Set(ResultsChoice.Menu));
            _again.OnClick(() => _choice.Set(ResultsChoice.Again));
            _double.OnClick(() => _choice.Set(ResultsChoice.DoubleReward));
        }

        /// <param name="notes">Дополнительные строки под счётом (звёзды за соперника и т.п.).</param>
        /// <param name="againText">Подпись второй кнопки («Дальше», «Переиграть этаж»); пусто — «Ещё раз».</param>
        public void Setup(ZonkMatch match, string title, Color titleColor, IReadOnlyList<GrantedReward> rewards, bool canDouble,
            bool canAgain, IReadOnlyList<string> notes = null, string againText = null)
        {
            _title.text = title;
            _title.color = titleColor;
            _menu.SetText(T("results.menu"));
            _again.SetText(string.IsNullOrEmpty(againText) ? T("results.again") : againText);
            _double.SetText(T("results.double"));
            _again.SetVisible(canAgain);
            _double.SetVisible(canDouble);

            if (_headline != null)
            {
                var scores = new string[match.Players.Count];
                for (var i = 0; i < scores.Length; i++)
                    scores[i] = match.Players[i].Score.ToString();
                _headline.text = string.Join(" : ", scores);
            }

            // С крупным счётом очки в строках не повторяем.
            foreach (var player in match.Players)
            {
                Spawn(_lineTemplate, _lines).text = _headline != null
                    ? T("results.lineShort", player.Name, player.BestTurn, player.ZonkCount)
                    : T("results.line", player.Name, player.Score, player.BestTurn, player.ZonkCount);
            }

            if (notes != null)
            {
                foreach (var note in notes)
                    Spawn(_lineTemplate, _lines).text = note;
            }

            ShowRewards(rewards);
        }

        /// <summary>Плашка-взрыв поверх итогов. Кнопки под ней работают: не надо ждать конца показа.</summary>
        public UniTask PlayBurstAsync(string word, string caption, Color color, bool fire, float hold, CancellationToken ct)
        {
            return _burst != null ? _burst.PlayAsync(word, caption, color, fire, hold, ct) : UniTask.CompletedTask;
        }

        /// <summary>На время плашки заголовок прячется: то же слово дважды выглядит ошибкой. Место в окне он держит.</summary>
        public void SetTitleVisible(bool visible)
        {
            if (_title != null)
                _title.enabled = visible;
        }

        /// <summary>Блок «Дальше»: ближайшая цель игрока. Пусто — блока нет.</summary>
        public void SetNextGoal(string title, string text)
        {
            if (_nextGoal == null)
                return;

            _nextGoal.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text))
                _nextGoal.Label.text = "<b><color=#" + ColorUtility.ToHtmlStringRGB(UiColors.Bad) + ">" + title + "</color></b>\n" + text;
        }

        /// <summary>Строка про заработанное за партию: достижение, задание, сундук. Цвет — как у важности.</summary>
        public void AddLine(string text, Color color)
        {
            var line = Spawn(_lineTemplate, _lines);
            line.text = text;
            line.color = color;
        }

        public void ShowRewards(IReadOnlyList<GrantedReward> rewards)
        {
            foreach (Transform child in _rewards)
            {
                if (child != _rewardTemplate.transform)
                    Destroy(child.gameObject);
            }

            if (rewards == null)
                return;

            // Монеты одной строкой: «+30, +30, +60» читается хуже, чем «+120».
            var totals = new Dictionary<CurrencyConfig, int>();
            var order = new List<CurrencyConfig>();
            foreach (var reward in rewards)
            {
                if (reward.Currency == null)
                    continue;
                if (!totals.ContainsKey(reward.Currency))
                {
                    totals[reward.Currency] = 0;
                    order.Add(reward.Currency);
                }

                totals[reward.Currency] += reward.Amount;
            }

            foreach (var currency in order)
                Spawn(_rewardTemplate, _rewards).text = "+" + totals[currency] + " " + T(currency.NameKey);

            foreach (var reward in rewards)
            {
                if (reward.Item != null)
                    Spawn(_rewardTemplate, _rewards).text = T("results.unlocked", RewardNames.Describe(reward.Item, T));
            }
        }

        /// <summary>Звёзды за соперника после партии: картинками (Parts/Stars), в старом префабе — строкой.</summary>
        public void ShowStars(int mask, int count)
        {
            if (count <= 0)
                return;

            if (_stars != null)
            {
                _stars.gameObject.SetActive(true);
                _stars.Show(mask, count, _uiConfig);
                return;
            }

            Spawn(_lineTemplate, _lines).text = T("results.stars", StarsText.Render(mask, count));
        }

        /// <summary>Опыт за партию: полоса уровня заполняется, в старом префабе — строкой.</summary>
        public void ShowXp(XpGain gain, IPlayerLevel level)
        {
            if (gain.Total <= 0 && gain.XpBefore == gain.XpAfter)
                return;

            if (_playerLevel != null)
            {
                _playerLevel.gameObject.SetActive(true);
                _playerLevel.PlayGain(gain, level);
                return;
            }

            Spawn(_lineTemplate, _lines).text = T("level.xpGained", gain.Total);
        }

        /// <summary>Монеты за партию уже выданы: кошелёк окна показывает их полётом и накруткой.</summary>
        public void PlayCoinsGained(IReadOnlyList<GrantedReward> rewards, CurrencyConfig coins)
        {
            var wallet = GetComponentInChildren<Views.WalletView>();
            if (wallet == null || rewards == null)
                return;

            var amount = 0;
            foreach (var reward in rewards)
            {
                if (reward.Currency != null && reward.Currency == coins)
                    amount += reward.Amount;
            }

            wallet.PlayGain(amount);
        }

        public void HideDouble()
        {
            _double.SetVisible(false);
        }

        public UniTask<ResultsChoice> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }

        private static TMP_Text Spawn(TMP_Text template, RectTransform parent)
        {
            var copy = Instantiate(template, parent);
            copy.gameObject.SetActive(true);
            return copy;
        }
    }
}
