using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Utils;

namespace Zonk.Presentation
{
    /// <summary>
    /// Камера стола: плавный перелёт между ракурсами и тряска. Корень двигается по ракурсам,
    /// сама камера — дочерний объект, её локальное смещение используется только для тряски.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private List<CameraShot> _shots = new List<CameraShot>();

        private CancellationTokenSource _moveCts;
        private CancellationTokenSource _shakeCts;

        public Camera Camera => _camera;
        [Tooltip("Соотношение сторон, под которое выставлены ракурсы. На более узком экране обзор расширяется")]
        [SerializeField] private float _designAspect = 16f / 9f;

        private float _designFov = 45f;

        public string CurrentShot { get; private set; }

#if UNITY_EDITOR
        public void EditorSetup(Camera camera, List<CameraShot> shots)
        {
            _camera = camera;
            _shots = shots;
        }
#endif

        /// <summary>
        /// Угол обзора камеры задан по вертикали. На экране уже, чем 16:9 (квадрат, телефон вертикально),
        /// по бокам обрезался бы стол, поэтому вертикальный угол растёт так, чтобы по ширине было видно столько же.
        /// </summary>
        private void LateUpdate()
        {
            _camera.fieldOfView = FitFov(_designFov, _camera.aspect, _designAspect);
        }

        public static float FitFov(float verticalFov, float aspect, float designAspect)
        {
            if (aspect >= designAspect || aspect <= 0f)
                return verticalFov;

            var halfWidth = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * designAspect;
            return Mathf.Min(2f * Mathf.Atan(halfWidth / aspect) * Mathf.Rad2Deg, 120f);
        }

        private void OnDestroy()
        {
            Cancel(ref _moveCts);
            Cancel(ref _shakeCts);
        }

        public CameraShot Find(string id)
        {
            foreach (var shot in _shots)
            {
                if (shot != null && shot.Id == id)
                    return shot;
            }

            return null;
        }

        public void SnapTo(string id)
        {
            var shot = Find(id);
            if (shot == null)
                return;

            Cancel(ref _moveCts);
            transform.SetPositionAndRotation(shot.transform.position, shot.transform.rotation);
            _designFov = shot.FieldOfView;
            CurrentShot = id;
        }

        /// <summary>Перелёт к ракурсу. Новый перелёт отменяет предыдущий, камера летит из текущей точки.</summary>
        public async UniTask MoveToAsync(string id, float duration, CancellationToken ct)
        {
            var shot = Find(id);
            if (shot == null)
            {
                Debug.LogWarning($"[Camera] Shot '{id}' not found");
                return;
            }

            Cancel(ref _moveCts);
            _moveCts = CancellationTokenSource.CreateLinkedTokenSource(ct, this.GetCancellationTokenOnDestroy());
            var token = _moveCts.Token;
            CurrentShot = id;

            var fromPosition = transform.position;
            var fromRotation = transform.rotation;
            var fromFov = _designFov;
            var toFov = shot.FieldOfView;

            await Animate.RunAsync(duration, t =>
            {
                transform.position = Vector3.LerpUnclamped(fromPosition, shot.transform.position, t);
                transform.rotation = Quaternion.SlerpUnclamped(fromRotation, shot.transform.rotation, t);
                _designFov = Mathf.LerpUnclamped(fromFov, toFov, t);
            }, token, AnimateEase.InOutCubic).SuppressCancellationThrow();
        }

        public void Shake(float amplitude, float duration)
        {
            Cancel(ref _shakeCts);
            _shakeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ShakeAsync(amplitude, duration, _shakeCts.Token).Forget();
        }

        private async UniTaskVoid ShakeAsync(float amplitude, float duration, CancellationToken ct)
        {
            var cameraTransform = _camera.transform;
            await Animate.RunAsync(duration, t =>
            {
                var strength = amplitude * (1f - t);
                cameraTransform.localPosition = Random.insideUnitSphere * strength;
            }, ct, AnimateEase.Linear).SuppressCancellationThrow();

            cameraTransform.localPosition = Vector3.zero;
        }

        private static void Cancel(ref CancellationTokenSource source)
        {
            if (source == null)
                return;

            source.Cancel();
            source.Dispose();
            source = null;
        }
    }
}
