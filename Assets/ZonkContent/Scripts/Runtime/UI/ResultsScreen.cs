using TMPro;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Core.Match;
using Zonk.Progress;

namespace Zonk.UI
{
    public enum ResultsChoice
    {
        Again,
        Menu,
        DoubleReward,
    }

    /// <summary>Итоги партии: победитель, счёт, награды, «Ещё раз» и «В меню».</summary>
    public sealed class ResultsScreen : UiScreen
    {
        protected override bool IsPopup => true;

        private readonly Choice<ResultsChoice> _choice = new Choice<ResultsChoice>();
        private readonly RectTransform _rewards;
        private UiButton _double;

        public ResultsScreen(UiKit kit, RectTransform parent, ZonkMatch match, string title, Color titleColor,
            IReadOnlyList<GrantedReward> rewards, bool canDouble, bool canAgain) : base(kit, parent)
        {
            kit.Blocker(Root, 0.4f);
            var panel = kit.Panel("Results", Root, UiColors.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 720));
            var column = kit.Column(panel.transform, 14, 32);
            UiKit.Stretch((RectTransform)column.transform);

            UiKit.Size(kit.Label(column.transform, title, 56, TextAnchor.MiddleCenter, titleColor), -1, 100);

            foreach (var player in match.Players)
            {
                var line = kit.T("results.line", player.Name, player.Score, player.BestTurn, player.ZonkCount);
                UiKit.Size(kit.Label(column.transform, line, 28), -1, 50);
            }

            var rewardsColumn = kit.Column(column.transform, 6, 0);
            _rewards = (RectTransform)rewardsColumn.transform;
            ShowRewards(rewards);

            var buttons = kit.Row(column.transform, 20);
            UiKit.Size(buttons, -1, 84);
            kit.Button(buttons.transform, kit.T("results.menu"), () => _choice.Set(ResultsChoice.Menu), UiColors.ButtonMuted);
            if (canAgain)
                kit.Button(buttons.transform, kit.T("results.again"), () => _choice.Set(ResultsChoice.Again), UiColors.ButtonAccent);

            if (canDouble)
            {
                _double = kit.Button(column.transform, kit.T("results.double"), () => _choice.Set(ResultsChoice.DoubleReward),
                    UiColors.Button, 28);
            }
        }

        public void ShowRewards(IReadOnlyList<GrantedReward> rewards)
        {
            foreach (Transform child in _rewards)
                Object.Destroy(child.gameObject);

            if (rewards == null)
                return;

            foreach (var reward in rewards)
            {
                string text;
                if (reward.Currency != null)
                    text = "+" + reward.Amount + " " + Kit.T(reward.Currency.NameKey);
                else if (reward.Item != null)
                    text = Kit.T("results.unlocked", Kit.T(reward.Item.NameKey));
                else
                    continue;

                UiKit.Size(Kit.Label(_rewards, text, 30, TextAnchor.MiddleCenter, UiColors.Good), -1, 44);
            }
        }

        public void HideDouble()
        {
            _double?.SetVisible(false);
        }

        public UniTask<ResultsChoice> RunAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }
    }
}
