using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Стакан. Модель любая, но дочерний объект "Mouth" обязателен: из него высыпаются кости.
    /// Компонент добавляется к экземпляру стакана автоматически, в префабы его ставить не нужно.
    /// </summary>
    public sealed class CupView : MonoBehaviour
    {
        public const string MouthName = "Mouth";

        private Transform _mouth;

        public Transform Mouth
        {
            get
            {
                if (_mouth == null)
                    _mouth = FindDeep(transform, MouthName) ?? transform;
                return _mouth;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;

            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
