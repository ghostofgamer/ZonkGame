using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zonk.UI.Rules
{
    /// <summary>
    /// Строка комбинации: картинки костей, название, очки. Лежит в префабе окна правил как шаблон,
    /// окно создаёт по копии на каждый пример. Картинки костей — первый Image внутри Dice, остальные копии.
    /// </summary>
    public sealed class RulesComboRow : MonoBehaviour
    {
        [SerializeField] private RectTransform _dice;
        [SerializeField] private Image _dieTemplate;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _points;

        private readonly List<Image> _images = new List<Image>();

#if UNITY_EDITOR
        public void EditorSetup(RectTransform dice, Image dieTemplate, TMP_Text name, TMP_Text points)
        {
            _dice = dice;
            _dieTemplate = dieTemplate;
            _name = name;
            _points = points;
        }
#endif

        public void Set(IReadOnlyList<int> faces, IReadOnlyList<Sprite> faceSprites, string name, string points)
        {
            _name.text = name;
            _points.text = points;

            if (_images.Count == 0)
                _images.Add(_dieTemplate);

            while (_images.Count < faces.Count)
                _images.Add(Instantiate(_dieTemplate, _dice));

            for (var i = 0; i < _images.Count; i++)
            {
                var visible = i < faces.Count;
                _images[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;

                var face = Mathf.Clamp(faces[i], 1, 6);
                _images[i].sprite = faceSprites != null && faceSprites.Count >= face ? faceSprites[face - 1] : null;
            }
        }
    }
}
