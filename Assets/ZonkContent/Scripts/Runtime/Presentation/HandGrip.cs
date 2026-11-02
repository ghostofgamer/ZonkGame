using UnityEngine;
using Zonk.Configs;

namespace Zonk.Presentation
{
    /// <summary>
    /// Расчёт хвата стакана пальцами (тот же, что в превью Tools/Blender/zonk_arms.py). Ось стакана — X руки,
    /// всё считается в плоскости поперёк неё: точка — (высота над центром ладони, вперёд вдоль пальцев), то есть
    /// (Y, Z) руки. Фаланга — угол над «вперёд» (радианы) и длина. Сгиб к ладони — плюс. Без выделения памяти.
    /// </summary>
    public static class HandGrip
    {
        private const int Iterations = 14;

        /// <summary>Центр стакана радиуса radius в плоскости руки: касается подушки ладони у костяшек, кисть наклонена.</summary>
        public static Vector2 CupCentre(float radius, HandConfig config)
        {
            var tilt = config.Tilt * Mathf.Deg2Rad;
            return new Vector2(-config.PalmOffset - radius * Mathf.Cos(tilt), config.PadForward + radius * Mathf.Sin(tilt));
        }

        /// <summary>
        /// Палец обхватывает окружность (centre, radius): суставы по очереди от костяшки. Фаланга, которая не дотянется
        /// до стенки, гнётся только до касательной (не ныряет к стакану); дотянется — гнётся, пока конец не ляжет
        /// на стенку; уже упирается — чуть разгибается (до maxOpen). Углы (радианы) — в angles[offset..offset+count).
        /// </summary>
        public static void BendFinger(Vector2 start, float[] bases, float[] lengths, int offset, int count, Vector2 centre,
            float radius, float maxBend, float maxOpen, float[] angles)
        {
            var p = start;
            var total = 0f;
            for (var i = 0; i < count; i++)
            {
                var k = offset + i;
                var direction = bases[k] - total;
                var length = lengths[k];
                var theta = float.NaN;

                var v = centre - p;
                var distance = v.magnitude;
                if (distance > radius)
                {
                    var tangent = Mathf.Atan2(v.x, v.y) + Mathf.Asin(radius / distance);
                    var toTangent = Mathf.Atan2(Mathf.Sin(direction - tangent), Mathf.Cos(direction - tangent));
                    if (toTangent >= 0f && length <= Mathf.Sqrt(distance * distance - radius * radius))
                        theta = Mathf.Min(toTangent, maxBend);
                }

                if (float.IsNaN(theta))
                {
                    theta = Outside(p, direction, length, 0f, centre, radius)
                        ? Crossing(p, direction, length, centre, radius, 0f, maxBend)
                        : Crossing(p, direction, length, centre, radius, 0f, -maxOpen);
                }

                angles[k] = theta;
                p = End(p, direction - theta, length);
                total += theta;
            }
        }

        /// <summary>Большой палец — один угол на все суставы: кончик на стенку; если он в стакане — отводится наружу.</summary>
        public static void BendThumb(Vector2 start, float[] bases, float[] lengths, int offset, int count, Vector2 centre,
            float radius, float maxBend, float maxOpen, float[] angles)
        {
            var hi = TipOutside(start, bases, lengths, offset, count, 0f, centre, radius) ? maxBend : -maxOpen;
            var lo = 0f;
            var startOutside = TipOutside(start, bases, lengths, offset, count, lo, centre, radius);
            float theta;
            if (TipOutside(start, bases, lengths, offset, count, hi, centre, radius) == startOutside)
            {
                theta = hi;
            }
            else
            {
                for (var i = 0; i < Iterations; i++)
                {
                    var mid = (lo + hi) * 0.5f;
                    if (TipOutside(start, bases, lengths, offset, count, mid, centre, radius) == startOutside)
                        lo = mid;
                    else
                        hi = mid;
                }

                theta = (lo + hi) * 0.5f;
            }

            for (var i = 0; i < count; i++)
                angles[offset + i] = theta;
        }

        /// <summary>Угол между from и to, где конец фаланги переходит через стенку; не переходит — to.</summary>
        private static float Crossing(Vector2 p, float direction, float length, Vector2 centre, float radius, float from, float to)
        {
            var startOutside = Outside(p, direction, length, from, centre, radius);
            if (Outside(p, direction, length, to, centre, radius) == startOutside)
                return to;

            for (var i = 0; i < Iterations; i++)
            {
                var mid = (from + to) * 0.5f;
                if (Outside(p, direction, length, mid, centre, radius) == startOutside)
                    from = mid;
                else
                    to = mid;
            }

            return (from + to) * 0.5f;
        }

        private static bool Outside(Vector2 p, float direction, float length, float theta, Vector2 centre, float radius)
        {
            return (End(p, direction - theta, length) - centre).sqrMagnitude > radius * radius;
        }

        private static bool TipOutside(Vector2 start, float[] bases, float[] lengths, int offset, int count, float theta,
            Vector2 centre, float radius)
        {
            var p = start;
            for (var i = 0; i < count; i++)
                p = End(p, bases[offset + i] - theta * (i + 1), lengths[offset + i]);
            return (p - centre).sqrMagnitude > radius * radius;
        }

        private static Vector2 End(Vector2 p, float angle, float length)
        {
            return new Vector2(p.x + Mathf.Sin(angle) * length, p.y + Mathf.Cos(angle) * length);
        }
    }
}
