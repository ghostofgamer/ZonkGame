using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Строка таблицы рекордов (префаб Prefabs/UI/Parts/LeaderboardRow): место, имя, результат; строка игрока
    /// подсвечена. Вид — в префабе.
    /// </summary>
    public sealed class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _rank;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _score;

#if UNITY_EDITOR
        public void EditorSetup(Image background, TMP_Text rank, TMP_Text name, TMP_Text score)
        {
            _background = background;
            _rank = rank;
            _name = name;
            _score = score;
        }
#endif

        public void Setup(int rank, string name, long score, bool isPlayer)
        {
            if (_rank != null)
                _rank.text = rank.ToString();
            if (_name != null)
                _name.text = name;
            if (_score != null)
                _score.text = score.ToString();

            var color = isPlayer ? UiColors.Gold : UiColors.Text;
            if (_name != null)
                _name.color = color;
            if (_background != null)
                _background.color = isPlayer ? UiColors.PanelLight : new Color(1f, 1f, 1f, 0.04f);
        }
    }
}
