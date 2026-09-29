using TMPro;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.UI
{
    public enum MainMenuChoice
    {
        Campaign,
        HotSeat,
        Shop,
        Settings,
        Rules,
    }

    /// <summary>Главное меню поверх стола: стол в текущей теме и есть витрина.</summary>
    public sealed class MainMenuScreen : UiScreen
    {
        private readonly Choice<MainMenuChoice> _choice = new Choice<MainMenuChoice>();

        public MainMenuScreen(UiKit kit, RectTransform parent, IWallet wallet, GameConfig config) : base(kit, parent)
        {
            var panel = kit.Panel("Menu", Root, UiColors.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(520, 720), new Vector2(48, 0));
            var column = kit.Column(panel.transform, 18, 28);
            UiKit.Stretch((RectTransform)column.transform);

            var title = kit.Label(column.transform, kit.T("game.title"), 52, TextAnchor.MiddleCenter, UiColors.Gold);
            UiKit.Size(title, -1, 150);

            kit.Button(column.transform, kit.T("menu.campaign"), () => _choice.Set(MainMenuChoice.Campaign), UiColors.ButtonAccent, 34);
            kit.Button(column.transform, kit.T("menu.hotseat"), () => _choice.Set(MainMenuChoice.HotSeat), null, 34);
            kit.Button(column.transform, kit.T("menu.shop"), () => _choice.Set(MainMenuChoice.Shop), null, 34);
            kit.Button(column.transform, kit.T("menu.settings"), () => _choice.Set(MainMenuChoice.Settings), UiColors.ButtonMuted, 30);

            // Книжка с вопросом в углу меню: правила игры и все комбинации.
            var rules = kit.IconButton(panel.transform, kit.Config != null ? kit.Config.RulesIcon : null, "?",
                () => _choice.Set(MainMenuChoice.Rules));
            UiKit.Place((RectTransform)rules.GameObject.transform, new Vector2(1f, 1f), new Vector2(96, 96), new Vector2(-16, -16));

            WalletBar.Create(kit, Root, wallet, config);
        }

        public UniTask<MainMenuChoice> RunAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }
    }

    /// <summary>Настройки: звук, музыка, скорость анимаций.</summary>
    public sealed class SettingsScreen : UiScreen
    {
        protected override bool IsPopup => true;

        private readonly Choice<bool> _close = new Choice<bool>();

        public SettingsScreen(UiKit kit, RectTransform parent, IGameSettings settings) : base(kit, parent)
        {
            kit.Blocker(Root);
            var panel = kit.Panel("Settings", Root, UiColors.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(620, 560));
            var column = kit.Column(panel.transform, 18, 32);
            UiKit.Stretch((RectTransform)column.transform);

            UiKit.Size(kit.Label(column.transform, kit.T("menu.settings"), 44, TextAnchor.MiddleCenter, UiColors.Gold), -1, 80);

            UiButton sound = null, music = null, speed = null;
            sound = kit.Button(column.transform, string.Empty, () =>
            {
                settings.Sound = !settings.Sound;
                Refresh();
            });
            music = kit.Button(column.transform, string.Empty, () =>
            {
                settings.Music = !settings.Music;
                Refresh();
            });
            speed = kit.Button(column.transform, string.Empty, () =>
            {
                settings.Speed = settings.Speed >= 2 ? 1 : 2;
                Refresh();
            });
            kit.Button(column.transform, kit.T("ui.back"), () => _close.Set(true), UiColors.ButtonMuted);

            void Refresh()
            {
                sound.SetText(kit.T(settings.Sound ? "settings.soundOn" : "settings.soundOff"));
                music.SetText(kit.T(settings.Music ? "settings.musicOn" : "settings.musicOff"));
                speed.SetText(kit.T("settings.speed", settings.Speed));
            }

            Refresh();
        }

        public UniTask<bool> RunAsync(CancellationToken ct)
        {
            return _close.WaitAsync(ct);
        }
    }

    /// <summary>Вопрос с двумя ответами.</summary>
    public sealed class ConfirmDialog : UiScreen
    {
        protected override bool IsPopup => true;

        private readonly Choice<bool> _answer = new Choice<bool>();

        public ConfirmDialog(UiKit kit, RectTransform parent, string text, string yes, string no) : base(kit, parent)
        {
            kit.Blocker(Root, 0.6f);
            var panel = kit.Panel("Confirm", Root, UiColors.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720, 340));
            var column = kit.Column(panel.transform, 24, 32);
            UiKit.Stretch((RectTransform)column.transform);
            UiKit.Size(kit.Label(column.transform, text, 32), -1, 140);
            var row = kit.Row(column.transform, 24);
            UiKit.Size(row, -1, 80);
            kit.Button(row.transform, yes, () => _answer.Set(true), UiColors.ButtonAccent);
            kit.Button(row.transform, no, () => _answer.Set(false), UiColors.ButtonMuted);
        }

        public UniTask<bool> RunAsync(CancellationToken ct)
        {
            return _answer.WaitAsync(ct);
        }

        public static async UniTask<bool> AskAsync(UiKit kit, RectTransform parent, string text, CancellationToken ct)
        {
            using (var dialog = new ConfirmDialog(kit, parent, text, kit.T("ui.yes"), kit.T("ui.no")))
                return await dialog.RunAsync(ct);
        }
    }
}
