using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Живой отсвет огня: полупрозрачное пятно поверх запечённого света неровно дышит.
    /// Настоящий источник не нужен, поэтому дешёвое и для телефонов. Числа — в префабе локации.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WorldGlowPulse : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _color = new Color(1f, 0.55f, 0.18f, 0.2f);

        [Tooltip("На сколько падает яркость в самый слабый момент, доля 0..1")]
        [SerializeField] private float _depth = 0.5f;

        [SerializeField] private float _speed = 2.3f;

#if UNITY_EDITOR
        public void EditorSetup(SpriteRenderer spriteRenderer, Color color, float depth, float speed)
        {
            _renderer = spriteRenderer;
            _color = color;
            _depth = depth;
            _speed = speed;
        }
#endif

        private void Update()
        {
            // Две несвязанные волны: огонь не должен дышать ровно, как маятник.
            var t = Time.time * _speed;
            var wave = 0.5f + 0.25f * Mathf.Sin(t) + 0.25f * Mathf.Sin(t * 2.7f + 1.3f);
            var color = _color;
            color.a *= 1f - _depth * wave;
            _renderer.color = color;
        }
    }
}
