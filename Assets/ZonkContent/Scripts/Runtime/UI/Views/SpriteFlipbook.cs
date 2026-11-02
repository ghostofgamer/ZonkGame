using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Покадровая анимация картинки: кадры сменяются по очереди, как у рисованного огня. Кадры и скорость — в префабе.
    /// Сдвиг кадра у копий одной детали, чтобы огоньки в ряду не двигались одинаково. Каждый кадр ничего не выделяет.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Sprite[] _frames = new Sprite[0];

        [Tooltip("Кадров в секунду")]
        [SerializeField] private float _framesPerSecond = 12f;

        [Tooltip("С какого кадра начинать: у соседних копий разный, чтобы огоньки не двигались одинаково")]
        [SerializeField] private int _frameOffset;

        private float _time;
        private int _shown = -1;

#if UNITY_EDITOR
        public void EditorSetup(Image image, Sprite[] frames, float framesPerSecond, int frameOffset)
        {
            _image = image;
            _frames = frames;
            _framesPerSecond = framesPerSecond;
            _frameOffset = frameOffset;
        }

        public void EditorSetOffset(int frameOffset)
        {
            _frameOffset = frameOffset;
        }
#endif

        private void OnEnable()
        {
            _time = 0f;
            _shown = -1;
            ShowFrame();
        }

        private void Update()
        {
            _time += Time.deltaTime;
            ShowFrame();
        }

        private void ShowFrame()
        {
            if (_frames.Length == 0 || _image == null)
                return;

            var frame = ((int)(_time * _framesPerSecond) + _frameOffset) % _frames.Length;
            if (frame == _shown)
                return;

            _shown = frame;
            _image.sprite = _frames[frame];
        }
    }
}
