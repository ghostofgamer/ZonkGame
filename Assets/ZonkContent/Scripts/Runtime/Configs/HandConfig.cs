using System;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Хват руки (HandView): где стакан относительно ладони, как пальцы его обхватывают, кулак и покой.
    /// Длины — в метрах сцены под модель Art/Models/Hands/Hand.fbx (Tools/Blender/zonk_arms.py, SCALE = 4):
    /// поменяли масштаб в скрипте — поменять и длины здесь (они там же, GRIP_*). Только показ.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Hand", fileName = "Hand")]
    public sealed class HandConfig : ScriptableObject
    {
        [Header("Где стакан")]
        [Tooltip("Низ ладони под центром руки (половина толщины ладони)")]
        public float PalmOffset = 0.137f;

        [Tooltip("Подушка ладони у костяшек: здесь стакан касается ладони, вперёд от центра руки")]
        public float PadForward = 0.186f;

        [Tooltip("Наклон кисти, градусы: стакан уходит под пальцы, ладонь у запястья отходит от стенки. " +
                 "Больше — пальцы обхватывают сильнее")]
        [Range(0f, 60f)] public float Tilt = 35f;

        [Tooltip("Где на стакане рука, градусы по окружности от стороны, откуда она пришла (своя — от камеры): " +
                 "правая обходит вправо. 0 — ладонь к нам, рука поперёк кадра; 90 — ладонь на правом боку стакана, " +
                 "пальцы обхватывают его спереди, предплечье назад к камере")]
        [Range(0f, 120f)] public float GripAround = 70f;

        [Header("Пальцы")]
        [Tooltip("Половина толщины пальца: палец лежит на стенке, а не входит в неё")]
        public float FingerPad = 0.072f;

        [Tooltip("Наибольший сгиб сустава, градусы")]
        [Range(0f, 120f)] public float MaxBend = 95f;

        [Tooltip("Насколько сустав может разогнуться, если фаланга упирается в стенку, градусы")]
        [Range(0f, 45f)] public float MaxOpen = 15f;

        [Tooltip("Большой палец: наибольший сгиб к стакану и отвод от него (если он в стакане), градусы на сустав")]
        [Range(0f, 90f)] public float ThumbBend = 30f;
        [Range(0f, 90f)] public float ThumbOpen = 45f;

        [Header("Позы")]
        [Tooltip("Рука без стакана: лёгкий сгиб пальцев и большого пальца, градусы на сустав")]
        public float RelaxBend = 10f;
        public float ThumbRelaxBend;

        [Tooltip("Рука тянется к стакану: пальцы чуть разогнуты, градусы на сустав (минус — разогнуты)")]
        public float ReachBend = -5f;

        [Tooltip("Кулак (удар по столу): сгиб первого, второго и третьего сустава, градусы")]
        public Vector3 FistBend = new Vector3(90f, 95f, 70f);

        [Tooltip("Большой палец в кулаке: доля от сгиба пальцев")]
        [Range(0f, 1f)] public float ThumbFistShare = 0.4f;

        [Header("Сжатие на стакане")]
        [Tooltip("Насколько сжать пальцы на стакане: 0 — пальцы лежат на стенке (расчёт хвата), 1 — кулак (FistBend). " +
                 "В редакторе, пока стакан в руке, изменение видно сразу")]
        [Range(0f, 1f)] public float GripSqueeze;

        [Tooltip("Свой хват для отдельных стаканов (формы разные): сжатие и поправка радиуса. Стакана нет в списке — общие")]
        public CupGrip[] CupGrips;

        [Header("Движение")]
        [Tooltip("Скорость сгиба и разгиба пальцев, градусов в секунду")]
        public float CurlSpeed = 600f;

        /// <summary>Свой хват стакана cup или null. Без выделения памяти.</summary>
        public CupGrip FindCupGrip(CosmeticItemConfig cup)
        {
            if (cup == null || CupGrips == null)
                return null;

            for (var i = 0; i < CupGrips.Length; i++)
            {
                if (CupGrips[i] != null && CupGrips[i].Cup == cup)
                    return CupGrips[i];
            }

            return null;
        }

        private static HandConfig _fallback;

        public static HandConfig Fallback => _fallback != null ? _fallback : _fallback = CreateInstance<HandConfig>();
    }

    /// <summary>Хват одного стакана: заменяет общее сжатие и поправляет радиус, измеренный по габаритам модели.</summary>
    [Serializable]
    public sealed class CupGrip
    {
        [Tooltip("Предмет-стакан (конфиг косметики)")]
        public CosmeticItemConfig Cup;

        [Tooltip("Сжатие пальцев на этом стакане: 0 — на стенке, 1 — кулак")]
        [Range(0f, 1f)] public float Squeeze;

        [Tooltip("Поправка радиуса хвата, метры: радиус берётся по самому широкому месту модели (ручки, кайма). " +
                 "Минус — ладонь ближе к оси стакана, пальцы обхватывают уже")]
        [Range(-0.3f, 0.3f)] public float RadiusOffset;
    }
}
