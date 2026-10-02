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

            foreach (var player in match.Players)
                Spawn(_lineTemplate, _lines).text = T("results.line", player.Name, player.Score, player.BestTurn, player.ZonkCount);

            if (notes != null)
            {
                foreach (var note in notes)
                    Spawn(_lineTemplate, _lines).text = note;
            }

            ShowRewards(rewards);
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

            foreach (var reward in rewards)
            {
                string text;
                if (reward.Currency != null)
                    text = "+" + reward.Amount + " " + T(reward.Currency.NameKey);
                else if (reward.Item != null)
                    text = T("results.unlocked", RewardNames.Describe(reward.Item, T));
                else
                    continue;

                Spawn(_rewardTemplate, _rewards).text = text;
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
