using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using Zonk.UI.Windows;

namespace Zonk.UI
{
    /// <summary>
    /// Кнопка «Назад» Android и Escape (Input System приводит их к одной клавише): действует верхнее окно.
    /// Окно закрывается своей кнопкой, в партии открывается пауза. В главном меню на Android — вопрос о выходе,
    /// в браузере выйти некуда.
    /// </summary>
    public sealed class UiBackButton : ITickable, IDisposable
    {
        private readonly IUiService _ui;
        private readonly UiKit _kit;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _askingQuit;

        public UiBackButton(IUiService ui, UiKit kit)
        {
            _ui = ui;
            _kit = kit;
        }

        public void Tick()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            var top = _ui.Top;
            if (top == null || !top.AcceptsBack)
                return;

            if (top is MainMenuWindow)
            {
                if (Application.platform == RuntimePlatform.Android)
                    AskQuitAsync(_cts.Token).Forget();
                return;
            }

            top.OnBackPressed();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private async UniTaskVoid AskQuitAsync(CancellationToken ct)
        {
            if (_askingQuit)
                return;

            _askingQuit = true;
            try
            {
                if (await ConfirmWindow.AskAsync(_ui, _kit.T("menu.quitConfirm"), ct))
                    Application.Quit();
            }
            catch (OperationCanceledException)
            {
                // Закрылась сцена: спрашивать уже незачем.
            }
            finally
            {
                _askingQuit = false;
            }
        }
    }
}
