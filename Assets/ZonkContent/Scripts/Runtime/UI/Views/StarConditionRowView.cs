using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Условие звезды в окне кампании (префаб Prefabs/UI/Parts/StarConditionRow): картинка звезды и текст условия.
    /// Полученная — UiConfig.StarGold и цвет текста «выполнено», не полученная — StarGray и приглушённый текст.
    /// </summary>
    public sealed class StarConditionRowView : MonoBehaviour
    {
        [SerializeField] private Image _star;
        [SerializeField] private TMP_Text _text;

#if UNITY_EDITOR
        public void EditorSetup(Image star, TMP_Text text)
        {
            _star = star;
            _text = text;
        }
#endif

        public void Setup(bool got, string text, UiConfig ui)
        {
            if (_text != null)
            {
                _text.text = text;
                _text.color = got ? UiColors.Gold : UiColors.TextMuted;
            }

            if (_star == null)
                return;

            var sprite = ui != null ? (got ? ui.StarGold : ui.StarGray) : null;
            if (sprite != null)
            {
                _star.sprite = sprite;
                _star.color = Color.white;
            }
            else
            {
                _star.color = got ? UiColors.Gold : new Color(0.6f, 0.58f, 0.55f, 0.8f);
            }
        }
    }
}
