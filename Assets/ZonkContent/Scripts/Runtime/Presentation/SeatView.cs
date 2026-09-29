using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Место игрока за столом: где стоит стакан, где рука, куда откладываются кости, где табличка с именем.
    /// Юг — ближе к камере, север — напротив (соперник или второй игрок).
    /// </summary>
    public sealed class SeatView : MonoBehaviour
    {
        [SerializeField] private CosmeticAnchor _cupAnchor;
        [SerializeField] private HandView _hand;
        [SerializeField] private Transform _keptArea;
        [SerializeField] private Transform _shakePoint;
        [SerializeField] private Transform _bubbleAnchor;
        [SerializeField] private float _keptSpacing = 0.3f;

        public CosmeticAnchor CupAnchor => _cupAnchor;
        public HandView Hand => _hand;

        /// <summary>Точка над краем лотка, где стакан трясут и откуда высыпают кости.</summary>
        public Transform ShakePoint => _shakePoint;

        /// <summary>Где показывать облачко реплики.</summary>
        public Transform BubbleAnchor => _bubbleAnchor != null ? _bubbleAnchor : transform;

#if UNITY_EDITOR
        public void EditorSetup(CosmeticAnchor cupAnchor, HandView hand, Transform keptArea, Transform shakePoint,
            Transform bubbleAnchor)
        {
            _cupAnchor = cupAnchor;
            _hand = hand;
            _keptArea = keptArea;
            _shakePoint = shakePoint;
            _bubbleAnchor = bubbleAnchor;
        }
#endif

        /// <summary>Позиция отложенной кости номер index (0..5) в ряду перед игроком.</summary>
        public Vector3 KeptPosition(int index, float dieSize)
        {
            var offset = (index - 2.5f) * Mathf.Max(_keptSpacing, dieSize * 1.25f);
            var position = _keptArea.position + _keptArea.right * offset;
            position.y = _keptArea.position.y + dieSize * 0.5f;
            return position;
        }

        public Quaternion KeptYaw => Quaternion.Euler(0f, _keptArea.eulerAngles.y, 0f);
    }
}
