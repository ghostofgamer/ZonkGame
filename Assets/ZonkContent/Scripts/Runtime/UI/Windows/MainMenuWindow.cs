using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    public enum MainMenuChoice
    {
        Campaign,
        HotSeat,
        Shop,
        Settings,
        Rules,
    }

    /// <summary>Главное меню поверх стола: стол в текущей теме и есть витрина. Кошелёк — WalletView в префабе.</summary>
    public sealed class MainMenuWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private UiButtonView _campaign;
        [SerializeField] private UiButtonView _hotSeat;
        [SerializeField] private UiButtonView _shop;
        [SerializeField] private UiButtonView _settings;
        [SerializeField] private UiButtonView _rules;

        private readonly Choice<MainMenuChoice> _choice = new Choice<MainMenuChoice>();

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, UiButtonView campaign, UiButtonView hotSeat, UiButtonView shop,
            UiButtonView settings, UiButtonView rules)
        {
            _title = title;
            _campaign = campaign;
            _hotSeat = hotSeat;
            _shop = shop;
            _settings = settings;
            _rules = rules;
        }
#endif

        private void Awake()
        {
            _campaign.OnClick(() => _choice.Set(MainMenuChoice.Campaign));
            _hotSeat.OnClick(() => _choice.Set(MainMenuChoice.HotSeat));
            _shop.OnClick(() => _choice.Set(MainMenuChoice.Shop));
            _settings.OnClick(() => _choice.Set(MainMenuChoice.Settings));
            _rules.OnClick(() => _choice.Set(MainMenuChoice.Rules));
        }

        protected override void OnShowing()
        {
            _title.text = T("game.title");
            _campaign.SetText(T("menu.campaign"));
            _hotSeat.SetText(T("menu.hotseat"));
            _shop.SetText(T("menu.shop"));
            _settings.SetText(T("menu.settings"));
        }

        public UniTask<MainMenuChoice> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }
    }
}
