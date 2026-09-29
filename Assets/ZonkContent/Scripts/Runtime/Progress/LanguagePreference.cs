using System;
using System.Threading;
using Base.Core.Localization;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using Zenject;

namespace Zonk.Progress
{
    /// <summary>
    /// Язык, выбранный игроком. При запуске язык ставит площадка (требование Яндекса: автоматически по языку
    /// игрока), затем, когда загрузилось сохранение, — выбранный в настройках, если он есть и поддерживается.
    /// </summary>
    public sealed class LanguagePreference : IInitializable, IDisposable
    {
        private readonly ISaveStore _saves;
        private readonly IGameSettings _settings;
        private readonly ILocalization _localization;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public LanguagePreference(ISaveStore saves, IGameSettings settings, ILocalization localization)
        {
            _saves = saves;
            _settings = settings;
            _localization = localization;
        }

        public void Initialize()
        {
            ApplyWhenLoadedAsync(_cts.Token).Forget();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        /// <summary>Сменить язык сейчас и запомнить выбор.</summary>
        public void Choose(string code)
        {
            if (string.IsNullOrEmpty(code))
                return;

            _settings.Language = code;
            _localization.SetLanguage(code);
        }

        private async UniTaskVoid ApplyWhenLoadedAsync(CancellationToken ct)
        {
            if (await _saves.WaitLoadedAsync(ct).SuppressCancellationThrow())
                return;

            var chosen = _settings.Language;
            if (string.IsNullOrEmpty(chosen) || chosen == _localization.Language)
                return;

            foreach (var language in _localization.Languages)
            {
                if (language == chosen)
                {
                    _localization.SetLanguage(chosen);
                    return;
                }
            }
        }
    }
}
