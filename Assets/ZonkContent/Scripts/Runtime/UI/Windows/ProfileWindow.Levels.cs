using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Core.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Вкладка «Уровни» профиля: путь наград от 2-го уровня до предела (PlayerLevelConfig.LastLevel), строка на уровень
    /// (деталь Parts/LevelRewardRow) — вещи рубежа и монеты, пройденные отмечены, следующий подсвечен и прокручен в середину.
    /// Сверху — «Уровень 12 из 50» и ближайшая вещь. Всё из конфига: новый рубеж или поднятый предел появляются сами.
    /// Строится при открытии вкладки (редко: память выделять можно). Без полей в префабе вкладки нет.
    /// </summary>
    public sealed partial class ProfileWindow
    {
        [Header("Уровни (необязательно)")]
        [SerializeField] private UiButtonView _levelsTab;
        [SerializeField] private GameObject _levelsPage;
        [SerializeField] private TMP_Text _levelsSummary;
        [SerializeField] private RectTransform _levelsList;
        [SerializeField] private LevelRewardRowView _levelRowTemplate;

        private readonly List<LevelRewardRowView> _levelRows = new List<LevelRewardRowView>();
        private readonly List<Reward> _levelRewards = new List<Reward>();
        private readonly StringBuilder _levelText = new StringBuilder();

#if UNITY_EDITOR
        public void EditorSetupLevels(UiButtonView tab, GameObject page, TMP_Text summary, RectTransform list, LevelRewardRowView template)
        {
            _levelsTab = tab;
            _levelsPage = page;
            _levelsSummary = summary;
            _levelsList = list;
            _levelRowTemplate = template;
        }
#endif

        private PlayerLevelConfig LevelConfig => _gameConfig != null ? _gameConfig.PlayerLevel : null;

        private bool HasLevels => _levelsPage != null && _levelsList != null && _levelRowTemplate != null && _level != null &&
                                  LevelConfig != null;

        private void AwakeLevels()
        {
            if (_levelRowTemplate != null && _levelRowTemplate.gameObject.scene.IsValid())
                _levelRowTemplate.gameObject.SetActive(false);
            if (_levelsTab != null)
                _levelsTab.OnClick(() => ShowPage(Page.Levels));
        }

        private void ShowingLevels()
        {
            if (_levelsTab != null)
                _levelsTab.SetText(T("profile.levels"));
        }

        private void BuildLevels()
        {
            for (var i = 0; i < _levelRows.Count; i++)
                Destroy(_levelRows[i].gameObject);
            _levelRows.Clear();

            var config = LevelConfig;
            _level.GetProgress(_level.Xp, out var current, out var into, out var toNext);
            var last = config.LastLevel;
            var totalXp = _level.Xp;
            LevelRewardRowView nextRow = null;
            ContentConfig nextItem = null;
            var nextItemLevel = 0;

            for (var number = 2; number <= last; number++)
            {
                CollectRewards(config, number);
                var item = MainItem(out var accent);
                var state = number <= current ? LevelRowState.Reached : number == current + 1 ? LevelRowState.Next : LevelRowState.Ahead;

                string stateText;
                if (state == LevelRowState.Reached)
                    stateText = T("profile.levelReached");
                else
                {
                    var need = PlayerLevelMath.TotalXpFor(number, config.FirstLevelXp, config.LevelXpGrowth) - totalXp;
                    stateText = T("profile.levelXpLeft", need > 0 ? need : 0);
                }

                var row = Instantiate(_levelRowTemplate, _levelsList);
                row.gameObject.SetActive(true);
                row.Show(number, RewardsText(), stateText, IconOf(item), state, accent, _palette);
                _levelRows.Add(row);

                if (state == LevelRowState.Next)
                    nextRow = row;
                if (item != null && state != LevelRowState.Reached && nextItem == null)
                {
                    nextItem = item;
                    nextItemLevel = number;
                }
            }

            if (_levelsSummary != null)
            {
                var summary = config.MaxLevel > 0 ? T("profile.levelsSummary", current, config.MaxLevel) : T("stats.levelValue", current, into, toNext);
                if (nextItem != null)
                    summary += "  ·  " + T("profile.levelsNextItem", RewardNames.Describe(nextItem, T), nextItemLevel);
                else if (config.MaxLevel > 0 && current >= config.MaxLevel)
                    summary += "  ·  " + T("profile.levelsAllDone");
                _levelsSummary.text = summary;
            }

            ScrollToLevel(nextRow);
        }

        /// <summary>Награды уровня: обычная за каждый уровень и рубеж.</summary>
        private void CollectRewards(PlayerLevelConfig config, int number)
        {
            _levelRewards.Clear();
            var milestone = config.MilestoneAt(number);
            if (milestone != null)
                _levelRewards.AddRange(milestone.Rewards);
            _levelRewards.AddRange(config.EveryLevelRewards);
        }

        /// <summary>Первая вещь среди наград (её значок в строке) и цвет её редкости.</summary>
        private ContentConfig MainItem(out Color accent)
        {
            for (var i = 0; i < _levelRewards.Count; i++)
            {
                if (_levelRewards[i] is ContentReward content && content.Item != null)
                {
                    accent = content.Item is CosmeticItemConfig cosmetic ? _palette.RarityColor(cosmetic.Rarity) : _palette.Gold;
                    return content.Item;
                }
            }

            accent = _palette.Gold;
            return null;
        }

        /// <summary>«Аватар «Подкова» · 250 монет, 3 энергии»: сначала вещи, потом валюта, одинаковая валюта сложена.</summary>
        private string RewardsText()
        {
            _levelText.Clear();
            for (var i = 0; i < _levelRewards.Count; i++)
            {
                if (_levelRewards[i] is ContentReward content && content.Item != null)
                {
                    if (_levelText.Length > 0)
                        _levelText.Append(", ");
                    _levelText.Append(RewardNames.Describe(content.Item, T));
                }
            }

            var items = _levelText.Length;
            for (var i = 0; i < _levelRewards.Count; i++)
            {
                if (!(_levelRewards[i] is CurrencyReward currency) || currency.Currency == null || IsCounted(currency.Currency, i))
                    continue;

                var amount = 0;
                for (var j = i; j < _levelRewards.Count; j++)
                {
                    if (_levelRewards[j] is CurrencyReward same && same.Currency == currency.Currency)
                        amount += same.Amount;
                }

                if (amount <= 0)
                    continue;
                _levelText.Append(_levelText.Length == 0 ? "" : _levelText.Length == items ? "  ·  " : ", ");
                _levelText.Append(amount).Append(' ').Append(T(currency.Currency.NameKey));
            }

            return _levelText.ToString();
        }

        private bool IsCounted(CurrencyConfig currency, int before)
        {
            for (var i = 0; i < before; i++)
            {
                if (_levelRewards[i] is CurrencyReward earlier && earlier.Currency == currency)
                    return true;
            }

            return false;
        }

        private static Sprite IconOf(ContentConfig item)
        {
            switch (item)
            {
                case CosmeticItemConfig cosmetic: return cosmetic.Icon != null ? cosmetic.Icon : cosmetic.Slot != null ? cosmetic.Slot.Icon : null;
                case DieConfig die: return die.Icon;
                default: return null;
            }
        }

        /// <summary>Следующий уровень — в середину списка (после раскладки, иначе высота ещё не посчитана).</summary>
        private void ScrollToLevel(LevelRewardRowView row)
        {
            var scroll = _levelsList.GetComponentInParent<ScrollRect>();
            if (scroll == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_levelsList);
            var viewport = scroll.viewport != null ? scroll.viewport.rect.height : ((RectTransform)scroll.transform).rect.height;
            var scrollable = _levelsList.rect.height - viewport;
            if (row == null || scrollable <= 0f)
            {
                scroll.verticalNormalizedPosition = row == null && _level.Level > 1 ? 0f : 1f;
                return;
            }

            // Раскладка ставит строку якорем к верху содержимого: -y — расстояние от верха до её середины.
            var top = -row.Rect.anchoredPosition.y - viewport * 0.5f;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / scrollable);
        }
    }
}
