using TMPro;
using UnityEngine;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Строка задания в окне заданий: текст, полоса прогресса, награда, кнопки «Забрать» и «Заменить».
    /// Шаблон строки — неактивный объект в префабе окна, строки — его копии (Spawn).
    /// </summary>
    public sealed class QuestRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private RectTransform _fill;
        [SerializeField] private TMP_Text _reward;
        [SerializeField] private UiButtonView _claim;
        [SerializeField] private UiButtonView _reroll;

        public UiButtonView Claim => _claim;
        public UiButtonView Reroll => _reroll;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, TMP_Text progress, RectTransform fill, TMP_Text reward, UiButtonView claim,
            UiButtonView reroll)
        {
            _title = title;
            _progress = progress;
            _fill = fill;
            _reward = reward;
            _claim = claim;
            _reroll = reroll;
        }
#endif

        public void Show(string title, int progress, int target, string reward, bool done)
        {
            _title.text = title;
            _progress.text = progress + " / " + target;
            _reward.text = reward;
            _title.color = done ? UiColors.Good : UiColors.Text;

            // Полоса: правый край заливки по доле прогресса, без спрайта с типом Filled.
            var fraction = target > 0 ? Mathf.Clamp01((float)progress / target) : 0f;
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(fraction, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
        }

        public QuestRowView Spawn(Transform parent)
        {
            var copy = Instantiate(this, parent);
            copy.gameObject.SetActive(true);
            return copy;
        }
    }
}
