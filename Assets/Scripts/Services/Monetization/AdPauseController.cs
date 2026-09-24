using System;
using Base.Platform;
using UnityEngine;
using Zenject;

namespace Base.Services.Monetization
{
    /// <summary>
    /// Пока показывается реклама, игра стоит на паузе и без звука. Это требование модерации
    /// Яндекс Игр и VK, поэтому оно сделано один раз здесь, а не в каждой игре.
    /// Звук также выключается, когда игра теряет фокус (свёрнута вкладка или приложение).
    ///
    /// Пауза через Time.timeScale = 0: игровая логика должна идти от scaled-времени,
    /// а анимации интерфейса, которые должны работать во время паузы, от unscaled.
    /// </summary>
    public sealed class AdPauseController : IInitializable, IDisposable
    {
        private readonly IAdsService _ads;
        private bool _adShowing;
        private bool _hasFocus = true;
        private float _timeScaleBeforeAd = 1f;

        public AdPauseController(IAdsService ads)
        {
            _ads = ads;
        }

        public void Initialize()
        {
            _ads.AdOpened += OnAdOpened;
            _ads.AdClosed += OnAdClosed;
            Application.focusChanged += OnFocusChanged;
        }

        public void Dispose()
        {
            _ads.AdOpened -= OnAdOpened;
            _ads.AdClosed -= OnAdClosed;
            Application.focusChanged -= OnFocusChanged;
        }

        private void OnAdOpened()
        {
            if (_adShowing)
                return;

            _adShowing = true;
            _timeScaleBeforeAd = Time.timeScale;
            Time.timeScale = 0f;
            UpdateAudio();
        }

        private void OnAdClosed()
        {
            if (!_adShowing)
                return;

            _adShowing = false;
            Time.timeScale = _timeScaleBeforeAd;
            UpdateAudio();
        }

        private void OnFocusChanged(bool hasFocus)
        {
            _hasFocus = hasFocus;
            UpdateAudio();
        }

        private void UpdateAudio()
        {
            AudioListener.pause = _adShowing || !_hasFocus;
        }
    }
}
