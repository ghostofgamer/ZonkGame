using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using Zonk.Configs;

namespace Zonk.UI
{
    /// <summary>
    /// Окна-префабы: открыть по типу, закрыть. Окно создаётся из префаба только при открытии
    /// и удаляется при закрытии. Префабы перечислены в UiConfig.Windows.
    /// Когда подключим Addressables, изменится только способ загрузки префаба внутри сервиса.
    /// </summary>
    public interface IUiService
    {
        /// <summary>Создать окно из префаба и показать. setup вызывается до анимации показа: заполнить окно данными.</summary>
        UniTask<T> OpenAsync<T>(CancellationToken ct, Action<T> setup = null) where T : UiWindow;
        UniTask CloseAsync(UiWindow window, CancellationToken ct);
    }

    public sealed class UiService : IUiService, IDisposable
    {
        private readonly UiConfig _config;
        private readonly RectTransform _root;
        private readonly DiContainer _container;
        private readonly List<UiWindow> _open = new List<UiWindow>();

        public UiService(GameConfig config, [Inject(Id = UiRootId)] RectTransform root, DiContainer container)
        {
            _config = config.Ui;
            _root = root;
            _container = container;
        }

        /// <summary>ID биндинга корневого RectTransform, в котором создаются окна.</summary>
        public const string UiRootId = "UiRoot";

        public async UniTask<T> OpenAsync<T>(CancellationToken ct, Action<T> setup = null) where T : UiWindow
        {
            var prefab = _config != null ? _config.FindWindow<T>() : null;
            if (prefab == null)
                throw new InvalidOperationException($"Window {typeof(T).Name} is not in UiConfig.Windows: its prefab is missing in Prefabs/UI. " +
                                                    "Run Zonk/Setup/Build Everything (or Zonk/Content/Rebuild UI Windows)");

            var window = _container.InstantiatePrefabForComponent<T>(prefab, _root);
            window.ApplyDefaults(_config);
            setup?.Invoke(window);
            _open.Add(window);
            await window.ShowAsync(ct);
            return window;
        }

        public async UniTask CloseAsync(UiWindow window, CancellationToken ct)
        {
            if (window == null)
                return;

            _open.Remove(window);
            try
            {
                await window.HideAsync(ct);
            }
            finally
            {
                if (window != null)
                    UnityEngine.Object.Destroy(window.gameObject);
            }
        }

        public void Dispose()
        {
            foreach (var window in _open)
            {
                if (window != null)
                    UnityEngine.Object.Destroy(window.gameObject);
            }

            _open.Clear();
        }
    }
}
