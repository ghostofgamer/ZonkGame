using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>Табличка игрока в HUD: имя его цветом, счёт, подсветка того, чей ход.</summary>
    public sealed class HudPlayerPanel : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _score;

#if UNITY_EDITOR
        public void EditorSetup(Image background, TMP_Text name, TMP_Text score)
        {
            _background = background;
            _name = name;
            _score = score;
        }
#endif

        public void SetPlayer(string name, Color color)
        {
            _name.text = name;
            _name.color = color;
        }

        public void SetScore(int score, bool active)
        {
            _score.text = score.ToString();
            _background.color = active ? UiColors.PanelLight : UiColors.Panel;
            transform.localScale = active ? Vector3.one * 1.06f : Vector3.one;
        }
    }
}
