using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Utils;

namespace Zonk.Presentation
{
    /// <summary>
    /// Соперник напротив: силуэт в полутени, голова с глазами, свободная рука для жестов.
    /// Сейчас заглушка из примитивов с процедурными жестами. Модель с аниматором заменит её,
    /// сохранив методы (жест = триггер аниматора с тем же именем).
    /// </summary>
    public sealed class OpponentAvatarView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform _body;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _accessoryAnchor;
        [SerializeField] private HandView _gestureHand;
        [SerializeField] private Renderer[] _bodyRenderers = Array.Empty<Renderer>();
        [SerializeField] private Transform _slamPoint;

        private GameObject _accessory;
        private Vector3 _bodyRest;
        private Quaternion _headRest;
        private MaterialPropertyBlock _block;
        private CancellationTokenSource _idleCts;

        /// <summary>Удар по столу: кости подпрыгивают, камера вздрагивает.</summary>
        public event Action TableImpact;

#if UNITY_EDITOR
        public void EditorSetup(Transform body, Transform head, Transform accessoryAnchor, HandView gestureHand,
            Renderer[] bodyRenderers, Transform slamPoint)
        {
            _body = body;
            _head = head;
            _accessoryAnchor = accessoryAnchor;
            _gestureHand = gestureHand;
            _bodyRenderers = bodyRenderers;
            _slamPoint = slamPoint;
        }
#endif

        private void Awake()
        {
            _bodyRest = _body.localPosition;
            _headRest = _head.localRotation;
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            StopIdle();
        }

        public void Show(OpponentConfig opponent)
        {
            gameObject.SetActive(true);

            foreach (var bodyRenderer in _bodyRenderers)
            {
                bodyRenderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, opponent != null ? opponent.BodyColor : Color.gray);
                bodyRenderer.SetPropertyBlock(_block);
            }

            if (_accessory != null)
                Destroy(_accessory);
            if (opponent != null && opponent.Accessory != null)
                _accessory = Instantiate(opponent.Accessory, _accessoryAnchor, false);

            StartIdle();
        }

        public void Hide()
        {
            StopIdle();
            gameObject.SetActive(false);
        }

        public async UniTask PlayAsync(AvatarGesture gesture, CancellationToken ct)
        {
            if (!gameObject.activeInHierarchy)
                return;

            switch (gesture)
            {
                case AvatarGesture.Think:
                    await TiltHeadAsync(new Vector3(0f, 0f, 14f), 0.9f, ct);
                    break;
                case AvatarGesture.Nod:
                    await TiltHeadAsync(new Vector3(18f, 0f, 0f), 0.5f, ct);
                    break;
                case AvatarGesture.Cheer:
                    await BounceAsync(0.25f, 3, 0.6f, ct);
                    break;
                case AvatarGesture.Laugh:
                    await BounceAsync(0.06f, 6, 0.8f, ct);
                    break;
                case AvatarGesture.Shrug:
                    await BounceAsync(0.12f, 1, 0.5f, ct);
                    break;
                case AvatarGesture.Angry:
                    await ShakeHeadAsync(0.6f, ct);
                    break;
                case AvatarGesture.SlamTable:
                    await SlamAsync(ct);
                    break;
            }
        }

        private async UniTask SlamAsync(CancellationToken ct)
        {
            if (_gestureHand == null || _slamPoint == null)
                return;

            var slam = _gestureHand.SlamAsync(_slamPoint.position, ct);
            await UniTask.Delay(260, cancellationToken: ct);
            TableImpact?.Invoke();
            await slam;
        }

        private async UniTask TiltHeadAsync(Vector3 euler, float duration, CancellationToken ct)
        {
            var target = _headRest * Quaternion.Euler(euler);
            await Animate.RotateLocalAsync(_head, target, duration * 0.35f, ct);
            await UniTask.Delay(TimeSpan.FromSeconds(duration * 0.3f), cancellationToken: ct);
            await Animate.RotateLocalAsync(_head, _headRest, duration * 0.35f, ct);
        }

        private async UniTask ShakeHeadAsync(float duration, CancellationToken ct)
        {
            await Animate.RunAsync(duration, t =>
                _head.localRotation = _headRest * Quaternion.Euler(0f, Mathf.Sin(t * Mathf.PI * 6f) * 20f * (1f - t), 0f),
                ct, AnimateEase.Linear);
            _head.localRotation = _headRest;
        }

        private async UniTask BounceAsync(float height, int times, float duration, CancellationToken ct)
        {
            await Animate.RunAsync(duration, t =>
                _body.localPosition = _bodyRest + Vector3.up * Mathf.Abs(Mathf.Sin(t * Mathf.PI * times)) * height,
                ct, AnimateEase.Linear);
            _body.localPosition = _bodyRest;
        }

        private void StartIdle()
        {
            StopIdle();
            _idleCts = new CancellationTokenSource();
            IdleAsync(_idleCts.Token).Forget();
        }

        private void StopIdle()
        {
            if (_idleCts == null)
                return;

            _idleCts.Cancel();
            _idleCts.Dispose();
            _idleCts = null;
        }

        /// <summary>Дыхание: лёгкое покачивание, чтобы силуэт не выглядел статуей.</summary>
        private async UniTaskVoid IdleAsync(CancellationToken ct)
        {
            var time = 0f;
            while (!ct.IsCancellationRequested)
            {
                time += Time.deltaTime;
                _body.localScale = new Vector3(1f, 1f + Mathf.Sin(time * 1.6f) * 0.012f, 1f);
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    return;
            }
        }
    }
}
