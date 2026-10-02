using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Узел дерева талантов (деталь Parts/TalentNode): подложка цветом состояния, значок, название, ранг «2/3».
    /// Закрытый (требования не выполнены) — серый; доступный — обычный; взятый — цветом ветки; полный — золотой.
    /// Нажатие — Clicked (окно показывает подробности и кнопку «Изучить»).
    /// </summary>
    public sealed class TalentNodeView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _rank;
        [SerializeField] private GameObject _selected;
        [SerializeField] private Button _button;

        public event Action Clicked;

#if UNITY_EDITOR
        public void EditorSetup(Image background, Image icon, TMP_Text name, TMP_Text rank, GameObject selected, Button button)
        {
            _background = background;
            _icon = icon;
            _name = name;
            _rank = rank;
            _selected = selected;
            _button = button;
        }
#endif

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void Show(Sprite icon, string title, int rank, int maxRank, Color background, Color iconColor, bool selected)
        {
            if (_background != null)
                _background.color = background;
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
                _icon.color = iconColor;
            }

            _name.text = title;
            if (_rank != null)
                _rank.text = rank + "/" + maxRank;
            if (_selected != null)
                _selected.SetActive(selected);
        }
    }
}
