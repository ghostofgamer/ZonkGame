using UnityEngine;

namespace Base.Services.Haptics
{
    /// <summary>
    /// Вибрация через системный Vibrator Android: VibrationEffect.createOneShot (Android 8+), иначе vibrate(ms).
    /// Handheld.Vibrate — запасной путь, если вызов Java не удался; заодно по нему Unity добавляет в манифест
    /// разрешение VIBRATE. На остальных платформах ничего не делает.
    /// </summary>
    public sealed class HapticsService : IHapticsService
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly AndroidJavaObject _vibrator;
        private readonly AndroidJavaClass _effects;
        private bool _broken;

        public HapticsService()
        {
            try
            {
                var activity = UnityEngine.Android.AndroidApplication.currentActivity;
                _vibrator = activity?.Call<AndroidJavaObject>("getSystemService", "vibrator");
                IsSupported = _vibrator != null && _vibrator.Call<bool>("hasVibrator");

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                        _effects = new AndroidJavaClass("android.os.VibrationEffect");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] Vibrator unavailable: " + e.Message);
                IsSupported = false;
            }
        }

        public bool IsSupported { get; }

        public void Pulse(int milliseconds, int amplitude = -1)
        {
            if (!IsSupported || milliseconds <= 0)
                return;

            if (_broken)
            {
                Handheld.Vibrate();
                return;
            }

            try
            {
                if (_effects != null)
                {
                    var strength = amplitude < 0 ? -1 : Mathf.Clamp(amplitude, 1, 255);
                    using (var effect = _effects.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, strength))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", (long)milliseconds);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] vibrate failed, falling back to Handheld.Vibrate: " + e.Message);
                _broken = true;
            }
        }
#else
        public bool IsSupported => false;

        public void Pulse(int milliseconds, int amplitude = -1)
        {
        }
#endif
    }
}
