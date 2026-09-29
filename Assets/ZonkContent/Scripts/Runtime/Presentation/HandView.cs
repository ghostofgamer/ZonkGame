using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Utils;

namespace Zonk.Presentation
{
    /// <summary>
    /// Рука, которая берёт стакан, трясёт и высыпает кости. Сейчас заглушка из примитивов с процедурной
    /// анимацией; модель руки с аниматором встанет сюда же, публичные методы не изменятся.
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] private Transform _grip;
        [SerializeField] private Transform _restPoint;

        [Tooltip("Правая рука: пальцы обхватывают стакан вправо. Выключить для левши")]
        [SerializeField] private bool _rightHanded = true;

        public Transform Grip => _grip;
        public bool RightHanded => _rightHanded;

#if UNITY_EDITOR
        public void EditorSetup(Transform grip, Transform restPoint, bool rightHanded = true)
        {
            _grip = grip;
            _restPoint = restPoint;
            _rightHanded = rightHanded;
        }
#endif

        public UniTask MoveToAsync(Vector3 position, Quaternion rotation, float duration, CancellationToken ct)
        {
            return Animate.PoseAsync(transform, position, rotation, duration, ct, AnimateEase.InOutCubic);
        }

        public UniTask ReturnAsync(float duration, CancellationToken ct)
        {
            return _restPoint == null
                ? UniTask.CompletedTask
                : Animate.PoseAsync(transform, _restPoint.position, _restPoint.rotation, duration, ct, AnimateEase.InOutCubic);
        }

        public void SnapToRest()
        {
            if (_restPoint != null)
                transform.SetPositionAndRotation(_restPoint.position, _restPoint.rotation);
        }

        /// <summary>
        /// Тряска вокруг текущей позы: частые смещения и покачивание. Частота, размах и наклон приходят из стиля броска,
        /// фаза каждой оси своя, поэтому тряска каждый раз немного другая.
        /// </summary>
        public async UniTask ShakeAsync(float duration, float amplitude, float frequency, float tilt, float seed,
            CancellationToken ct)
        {
            var basePosition = transform.position;
            var baseRotation = transform.rotation;
            await Animate.RunAsync(duration, t =>
            {
                var phase = t * duration * frequency + seed;
                var fade = Mathf.Sin(t * Mathf.PI);
                var offset = new Vector3(Mathf.Sin(phase), Mathf.Abs(Mathf.Sin(phase * 1.3f)) * 0.6f, Mathf.Cos(phase * 0.9f)) *
                             (amplitude * fade);
                transform.position = basePosition + offset;
                transform.rotation = baseRotation * Quaternion.Euler(Mathf.Sin(phase * 1.1f) * tilt * fade, 0f,
                    Mathf.Cos(phase + seed) * tilt * fade);
            }, ct, AnimateEase.Linear);

            transform.SetPositionAndRotation(basePosition, baseRotation);
        }

        /// <summary>Удар кулаком по столу.</summary>
        public async UniTask SlamAsync(Vector3 tablePoint, CancellationToken ct)
        {
            var start = transform.position;
            var up = tablePoint + Vector3.up * 0.6f;
            await Animate.MoveAsync(transform, up, 0.18f, ct, AnimateEase.OutCubic);
            await Animate.MoveAsync(transform, tablePoint, 0.08f, ct, AnimateEase.InCubic);
            await UniTask.Delay(120, cancellationToken: ct);
            await Animate.MoveAsync(transform, start, 0.3f, ct, AnimateEase.InOutCubic);
        }
    }
}
