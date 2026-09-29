using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
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
        [SerializeField] private UiButtonView _menu;
        [SerializeField] private UiButtonView _again;
        [SerializeField] private UiButtonView _double;

        private readonly Choice<ResultsChoice> _choice = new Choice<ResultsChoice>();

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform lines, TMP_Text lineTemplate, RectTransform rewards,
            TMP_Text rewardTemplate, UiButtonView menu, UiButtonView again, UiButtonView doubleReward)
        {
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

        private void Awake()
        {
            _lineTemplate.gameObject.SetActive(false);
            _rewardTemplate.gameObject.SetActive(false);
            _menu.OnClick(() => _choice.Set(ResultsChoice.Menu));
            _again.OnClick(() => _choice.Set(ResultsChoice.Again));
            _double.OnClick(() => _choice.Set(ResultsChoice.DoubleReward));
        }

        public void Setup(ZonkMatch match, string title, Color titleColor, IReadOnlyList<GrantedReward> rewards, bool canDouble,
            bool canAgain)
        {
            _title.text = title;
            _title.color = titleColor;
            _menu.SetText(T("results.menu"));
            _again.SetText(T("results.again"));
            _double.SetText(T("results.double"));
            _again.SetVisible(canAgain);
            _double.SetVisible(canDouble);

            foreach (var player in match.Players)
                Spawn(_lineTemplate, _lines).text = T("results.line", player.Name, player.Score, player.BestTurn, player.ZonkCount);

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
                    text = T("results.unlocked", T(reward.Item.NameKey));
                else
                    continue;

                Spawn(_rewardTemplate, _rewards).text = text;
            }
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
