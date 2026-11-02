using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Utils;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Плашка-взрыв из комикса (деталь Parts/ComicBurst): слово, подпись, цвет, по желанию кольцо огня.
    /// Вид и тайминги — в префабе, копию можно поставить в любое окно. Прячется сама, в сцене ничего не создаёт.
    /// </summary>
    public sealed class ComicBurstView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _shape;
        [SerializeField] private TMP_Text _word;
        [SerializeField] private TMP_Text _caption;

        [Tooltip("Кольцо огня вокруг плашки: включается для горячих костей")]
        [SerializeField] private GameObject _fire;

        [Tooltip("Плашка вылетает за столько секунд")]
        [SerializeField] private float _popDuration = 0.25f;

        [Tooltip("Плашка гаснет за столько секунд")]
        [SerializeField] private float _fadeDuration = 0.18f;

#if UNITY_EDITOR
        public void EditorSetup(CanvasGroup group, Image shape, TMP_Text word, TMP_Text caption, GameObject fire)
        {
            _group = group;
            _shape = shape;
            _word = word;
            _caption = caption;
            _fire = fire;
        }
#endif

        /// <param name="caption">Подпись под словом; пусто — без подписи.</param>
        /// <param name="hold">Сколько секунд плашка видна целиком.</param>
        public async UniTask PlayAsync(string word, string caption, Color color, bool fire, float hold, CancellationToken ct)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _shape.color = color;
            _word.text = word;
            _caption.gameObject.SetActive(!string.IsNullOrEmpty(caption));
            _caption.text = caption;
            if (_fire != null)
                _fire.SetActive(fire);
            _group.alpha = 1f;
            transform.localScale = Vector3.one * 0.15f;

            try
            {
                await Animate.ScaleAsync(transform, Vector3.one, _popDuration, ct, AnimateEase.OutBack);
                await UniTask.Delay(TimeSpan.FromSeconds(hold), cancellationToken: ct);

                var time = 0f;
                while (time < _fadeDuration)
                {
                    _group.alpha = 1f - time / _fadeDuration;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    time += Time.deltaTime;
                }
            }
            finally
            {
                // Окно могли закрыть посреди показа: вместе с ним уничтожена и плашка.
                if (this != null)
                    gameObject.SetActive(false);
            }
        }
    }
}
