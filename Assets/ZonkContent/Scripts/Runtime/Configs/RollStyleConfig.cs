using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>Параметры одного броска, выбранные из диапазонов стиля.</summary>
    public sealed class RollParams
    {
        public float ShakeDuration;
        public float ShakeAmplitude;
        public float ShakeFrequency;
        public float ShakeTilt;

        public float WindUpDistance;
        public float WindUpLift;
        public float WindUpTilt;
        public float WindUpDuration;
        public float WindUpHold;
        public float SwingDuration;
        public float FollowThrough;

        public float PourAngle;
        public float DirectionJitter;
        public float ThrowSpeed;
        public float SpinSpeed;
        public float Spread;
    }

    /// <summary>
    /// Стиль броска: тряска, замах к себе, рывок вперёд и высыпание. Каждый бросок берёт случайные значения
    /// из диапазонов, поэтому броски не повторяются. У соперника может быть свой стиль (OpponentConfig.RollStyle):
    /// осторожный почти не замахивается, азартный отводит стакан далеко и швыряет. На результат броска стиль
    /// не влияет: грани решает ГСЧ партии.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Roll Style", fileName = "RollStyle")]
    public sealed class RollStyleConfig : ScriptableObject
    {
        [Header("Тряска")]
        [Tooltip("Длительность тряски, секунды (от, до)")]
        public Vector2 ShakeDuration = new Vector2(0.55f, 0.85f);

        [Tooltip("Размах тряски в метрах сцены")]
        public Vector2 ShakeAmplitude = new Vector2(0.04f, 0.08f);

        [Tooltip("Частота тряски")]
        public Vector2 ShakeFrequency = new Vector2(18f, 26f);

        [Tooltip("Наклон стакана при тряске, градусы")]
        public Vector2 ShakeTilt = new Vector2(8f, 16f);

        [Header("Замах: стакан тянется к себе перед броском")]
        [Tooltip("Насколько отвести стакан назад, к игроку, метры сцены")]
        public Vector2 WindUpDistance = new Vector2(0.35f, 0.55f);

        [Tooltip("Насколько поднять стакан при замахе")]
        public Vector2 WindUpLift = new Vector2(0.05f, 0.15f);

        [Tooltip("Наклон горлышка назад при замахе, градусы")]
        public Vector2 WindUpTilt = new Vector2(15f, 30f);

        [Tooltip("Длительность замаха, секунды")]
        public Vector2 WindUpDuration = new Vector2(0.22f, 0.32f);

        [Tooltip("Пауза в верхней точке замаха, секунды: «прицеливание»")]
        public Vector2 WindUpHold = new Vector2(0.04f, 0.12f);

        [Header("Бросок")]
        [Tooltip("Длительность рывка вперёд, секунды. Меньше = резче")]
        public Vector2 SwingDuration = new Vector2(0.13f, 0.19f);

        [Tooltip("Насколько рука проходит дальше точки броска по инерции, метры сцены")]
        public Vector2 FollowThrough = new Vector2(0.12f, 0.25f);

        [Tooltip("Угол наклона стакана при высыпании, градусы")]
        public Vector2 PourAngle = new Vector2(95f, 120f);

        [Tooltip("Разброс направления броска от центра лотка, ± градусы")]
        public float DirectionJitter = 15f;

        [Tooltip("Скорость вылета костей")]
        public Vector2 ThrowSpeed = new Vector2(2.2f, 3.4f);

        [Tooltip("Скорость вращения костей")]
        public Vector2 SpinSpeed = new Vector2(8f, 18f);

        [Tooltip("Разброс костей у горла стакана, доля размера кости")]
        public Vector2 Spread = new Vector2(0.5f, 0.8f);

        private static RollStyleConfig _fallback;

        /// <summary>Стиль со значениями по умолчанию, если ассет не задан.</summary>
        public static RollStyleConfig Fallback => _fallback != null ? _fallback : _fallback = CreateInstance<RollStyleConfig>();

        public RollParams Pick(System.Random random)
        {
            return new RollParams
            {
                ShakeDuration = Range(ShakeDuration, random),
                ShakeAmplitude = Range(ShakeAmplitude, random),
                ShakeFrequency = Range(ShakeFrequency, random),
                ShakeTilt = Range(ShakeTilt, random),
                WindUpDistance = Range(WindUpDistance, random),
                WindUpLift = Range(WindUpLift, random),
                WindUpTilt = Range(WindUpTilt, random),
                WindUpDuration = Range(WindUpDuration, random),
                WindUpHold = Range(WindUpHold, random),
                SwingDuration = Range(SwingDuration, random),
                FollowThrough = Range(FollowThrough, random),
                PourAngle = Range(PourAngle, random),
                DirectionJitter = (float)(random.NextDouble() * 2 - 1) * DirectionJitter,
                ThrowSpeed = Range(ThrowSpeed, random),
                SpinSpeed = Range(SpinSpeed, random),
                Spread = Range(Spread, random),
            };
        }

        private static float Range(Vector2 range, System.Random random)
        {
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }
    }
}
