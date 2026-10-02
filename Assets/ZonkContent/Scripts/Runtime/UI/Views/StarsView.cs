using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Ряд звёзд картинками (префаб Prefabs/UI/Parts/Stars): сколько Image в префабе — столько звёзд можно показать.
    /// Полученная — спрайт UiConfig.StarGold, не полученная — StarGray. Вид (размер, расстояние, тень) настраивается
    /// в префабе, картинки — в UiConfig. Пока спрайтов нет, звёзды красятся цветом (золотой / серый).
    /// </summary>
    public sealed class StarsView : MonoBehaviour
    {
        [SerializeField] private Image[] _stars = new Image[0];

#if UNITY_EDITOR
        public void EditorSetup(Image[] stars)
        {
            _stars = stars;
        }
#endif

        /// <param name="mask">Полученные звёзды: бит i — звезда i.</param>
        /// <param name="count">Сколько звёзд всего (лишние Image скрываются).</param>
        public void Show(int mask, int count, UiConfig ui)
        {
            for (var i = 0; i < _stars.Length; i++)
            {
                var star = _stars[i];
                if (star == null)
                    continue;

                var visible = i < count;
                if (star.gameObject.activeSelf != visible)
                    star.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                Paint(star, (mask & (1 << i)) != 0, ui);
            }
        }

        /// <summary>Звезда картинкой: UiConfig.StarGold или StarGray, пока спрайтов нет — цветом (золотой / серый).</summary>
        public static void Paint(Image star, bool got, UiConfig ui)
        {
            var sprite = ui != null ? (got ? ui.StarGold : ui.StarGray) : null;
            if (sprite != null)
            {
                star.sprite = sprite;
                star.color = Color.white;
            }
            else
            {
                star.color = got ? UiColors.Gold : new Color(0.6f, 0.58f, 0.55f, 0.8f);
            }
        }
    }
}
