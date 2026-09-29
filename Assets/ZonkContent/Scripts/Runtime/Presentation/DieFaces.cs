using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Соглашение о гранях кости для всех моделей и скинов (локальные оси модели):
    /// 1 = +Y, 6 = −Y, 2 = +Z, 5 = −Z, 3 = +X, 4 = −X. Противоположные грани в сумме дают 7.
    /// UV-атлас: 3×2 ячейки, верхний ряд 1, 2, 3, нижний 4, 5, 6. Модель из Tools/Blender его соблюдает,
    /// валидатор контента проверяет новые меши костей.
    /// </summary>
    public static class DieFaces
    {
        private static readonly Vector3[] Normals =
        {
            Vector3.zero,
            Vector3.up,       // 1
            Vector3.forward,  // 2
            Vector3.right,    // 3
            Vector3.left,     // 4
            Vector3.back,     // 5
            Vector3.down,     // 6
        };

        public static Vector3 Normal(int face)
        {
            return Normals[Mathf.Clamp(face, 1, 6)];
        }

        /// <summary>Какая грань смотрит вверх при таком повороте модели.</summary>
        public static int UpFace(Quaternion rotation)
        {
            var best = 1;
            var bestDot = float.MinValue;
            for (var face = 1; face <= 6; face++)
            {
                var dot = Vector3.Dot(rotation * Normals[face], Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = face;
                }
            }

            return best;
        }

        /// <summary>Локальная ось тела (±X, ±Y, ±Z), которая смотрит вверх при повороте rotation.</summary>
        public static Vector3 UpAxis(Quaternion rotation)
        {
            return Normals[UpFace(rotation)];
        }

        /// <summary>Поворот модели, при котором грань face смотрит вверх, а поворот вокруг вертикали нулевой.</summary>
        public static Quaternion FaceUp(int face)
        {
            return FromTo(Normal(face), Vector3.up);
        }

        /// <summary>
        /// Поворот визуала относительно физического тела, чтобы вверх смотрела грань desiredFace,
        /// когда у тела вверх смотрит ось bodyUpAxis. Всегда симметрия куба (кратен 90°),
        /// поэтому коллайдер и физика броска не меняются.
        /// </summary>
        public static Quaternion Correction(int desiredFace, Vector3 bodyUpAxis)
        {
            return FromTo(Normal(desiredFace), bodyUpAxis);
        }

        /// <summary>FromToRotation для осевых векторов с явной обработкой противоположных.</summary>
        private static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            var dot = Vector3.Dot(from, to);
            if (dot > 0.99f)
                return Quaternion.identity;

            if (dot < -0.99f)
            {
                var axis = Mathf.Abs(from.y) > 0.5f ? Vector3.right : Vector3.up;
                return Quaternion.AngleAxis(180f, axis);
            }

            return Quaternion.FromToRotation(from, to);
        }
    }
}
