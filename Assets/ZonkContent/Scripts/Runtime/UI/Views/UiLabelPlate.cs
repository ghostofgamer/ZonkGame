using TMPro;
using UnityEngine;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Надпись на подложке (деталь Parts/LabelPlate): читается на любом фоне. Подложка растёт по тексту,
    /// вид и отступы — в префабе. Копию можно поставить куда угодно, Toast берёт её из пула.
    /// </summary>
    public sealed class UiLabelPlate : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private CanvasGroup _group;

        public TMP_Text Label => _label;
        public CanvasGroup Group => _group;
        public RectTransform Rect => (RectTransform)transform;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text label, CanvasGroup group)
        {
            _label = label;
            _group = group;
        }
#endif
    }
}
