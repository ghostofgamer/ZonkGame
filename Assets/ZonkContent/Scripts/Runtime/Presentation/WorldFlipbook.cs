using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Покадровая анимация картинки в сцене: огонь в камине, свечи. Кадры и скорость — в префабе локации.
    /// Те же кадры, что у огня в интерфейсе. Каждый кадр ничего не выделяет.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WorldFlipbook : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite[] _frames = new Sprite[0];
        [SerializeField] private float _framesPerSecond = 12f;

        [Tooltip("С какого кадра начинать: у соседних копий разный")]
        [SerializeField] private int _frameOffset;

        private float _time;
        private int _shown = -1;

#if UNITY_EDITOR
        public void EditorSetup(SpriteRenderer spriteRenderer, Sprite[] frames, float framesPerSecond, int frameOffset)
        {
            _renderer = spriteRenderer;
            _frames = frames;
            _framesPerSecond = framesPerSecond;
            _frameOffset = frameOffset;
        }
#endif

        private void Update()
        {
            if (_frames.Length == 0)
                return;

            _time += Time.deltaTime;
            var frame = ((int)(_time * _framesPerSecond) + _frameOffset) % _frames.Length;
            if (frame == _shown)
                return;

            _shown = frame;
            _renderer.sprite = _frames[frame];
        }
    }
}
