using TMPro;
using UnityEngine;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Строка статистики (деталь Parts/StatRow): название слева, значение справа. Заголовок раздела — та же строка
    /// без значения, цветом заголовка.
    /// </summary>
    public sealed class StatRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _value;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text label, TMP_Text value)
        {
            _label = label;
            _value = value;
        }
#endif

        public void SetRow(string label, string value, Color labelColor, Color valueColor)
        {
            _label.text = label;
            _label.color = labelColor;
            _label.fontStyle = FontStyles.Normal;
            if (_value != null)
            {
                _value.gameObject.SetActive(true);
                _value.text = value;
                _value.color = valueColor;
            }
        }

        public void SetHeader(string title, Color color)
        {
            _label.text = title;
            _label.color = color;
            _label.fontStyle = FontStyles.Bold;
            if (_value != null)
                _value.gameObject.SetActive(false);
        }
    }
}
