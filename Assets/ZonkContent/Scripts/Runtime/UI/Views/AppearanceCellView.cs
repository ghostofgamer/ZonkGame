using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Ячейка «Облика» в профиле (деталь Parts/AppearanceCell): аватар или рамка, подложка цветом редкости.
    /// Не открытый предмет — серый с замком, надетый — с отметкой. Нажатие — Clicked (надеть или подсказка, где взять).
    /// </summary>
    public sealed class AppearanceCellView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _picture;

        [Tooltip("Затемнение и замок поверх не открытого предмета")]
        [SerializeField] private GameObject _locked;

        [Tooltip("Отметка надетого предмета")]
        [SerializeField] private GameObject _equipped;

        [SerializeField] private Button _button;

        public event Action Clicked;

#if UNITY_EDITOR
        public void EditorSetup(Image background, Image picture, GameObject locked, GameObject equipped, Button button)
        {
            _background = background;
            _picture = picture;
            _locked = locked;
            _equipped = equipped;
            _button = button;
        }
#endif

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void Show(Sprite picture, Color rarity, bool owned, bool equipped, Color lockedColor)
        {
            if (_picture != null)
            {
                _picture.sprite = picture;
                _picture.enabled = picture != null;
                _picture.color = owned ? Color.white : lockedColor;
            }

            if (_background != null)
                _background.color = owned ? rarity : Color.Lerp(lockedColor, rarity, 0.25f);
            if (_locked != null)
                _locked.SetActive(!owned);
            if (_equipped != null)
                _equipped.SetActive(owned && equipped);
        }
    }
}
