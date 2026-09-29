using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Карточка товара в магазине (префаб Prefabs/UI/Parts/ShopCard): иконка из конфига предмета, название, цена
    /// (монеты / реклама / покупка) и состояние («надето», «есть»). Всё, что видно, настраивается в префабе, данные —
    /// в конфиге предмета (Icon, Price). Любое поле, кроме кнопки, можно убрать из префаба.
    /// </summary>
    public sealed class ShopCardView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private TMP_Text _state;

        [Tooltip("Рамка выбранной карточки")]
        [SerializeField] private GameObject _selection;

#if UNITY_EDITOR
        public void EditorSetup(Button button, Image background, Image icon, TMP_Text name, TMP_Text price, TMP_Text state,
            GameObject selection)
        {
            _button = button;
            _background = background;
            _icon = icon;
            _name = name;
            _price = price;
            _state = state;
            _selection = selection;
        }
#endif

        public void Setup(string name, Sprite icon, string price, string state, Color color, UnityAction onClick)
        {
            if (_name != null)
                _name.text = name;

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.gameObject.SetActive(icon != null);
            }

            SetText(_price, price);
            SetText(_state, state);

            if (_background != null)
                _background.color = color;

            _button.onClick.RemoveAllListeners();
            if (onClick != null)
                _button.onClick.AddListener(onClick);
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (_selection != null)
                _selection.SetActive(selected);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label == null)
                return;

            label.text = text;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
