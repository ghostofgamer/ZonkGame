using System;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using Zonk.UI;

namespace Zonk.Boot
{
    /// <summary>Имена сцен игры. Порядок в Build Settings: Bootstrap первой.</summary>
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string Table = "Table";
    }

    /// <summary>
    /// Крошечная первая сцена: пока виден экран загрузки, ProjectContext запускает SDK площадки, язык и сохранение.
    /// Затем грузится стол; экран загрузки переживает смену сцены, стол закрывает его сам, когда меню готово.
    /// </summary>
    public sealed class BootstrapEntry : MonoBehaviour
    {
        private static readonly TimeSpan SaveTimeout = TimeSpan.FromSeconds(15);

        private ISaveStore _saves;
        private LoadingScreenHolder _loading;

        [Inject]
        public void Construct(ISaveStore saves, LoadingScreenHolder loading)
        {
            _saves = saves;
            _loading = loading;
        }

        private void Start()
        {
            LoadAsync().Forget();
        }

        private async UniTaskVoid LoadAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();

            // Зависимостей нет: ProjectContext не собрался. Настоящая причина — первая ошибка Zenject в консоли.
            if (_saves == null || _loading == null)
            {
                Debug.LogError("[Zonk] Bootstrap was not injected: ProjectContext failed to install, see the first Zenject error above");
                return;
            }

            // Сохранение ограничено по времени внутри SaveStore; здесь запасной предел, чтобы не зависнуть навсегда.
            await _saves.WaitLoadedAsync(ct).TimeoutWithoutException(SaveTimeout);
            _loading.SetProgress(0.3f);

            var operation = SceneManager.LoadSceneAsync(SceneNames.Table);
            while (!operation.isDone)
            {
                _loading.SetProgress(0.3f + operation.progress * 0.6f);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }
}
