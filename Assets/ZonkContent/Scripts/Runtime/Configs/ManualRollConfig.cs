using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Бросок своей рукой: игрок зажимает свой стакан, трясёт его пальцем или мышью и толкает к столу.
    /// Кнопка «Бросить» при этом работает как раньше: оба способа доступны всегда. Жест меняет только полёт костей
    /// (направление, силу, закрутку), грани по-прежнему решает ГСЧ партии.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Manual Roll", fileName = "ManualRoll")]
    public sealed class ManualRollConfig : ScriptableObject
    {
        [Tooltip("Разрешить бросок своей рукой")]
        public bool Enabled = true;

        [Header("Захват")]
        [Tooltip("Зона захвата вокруг стакана: во сколько раз больше видимого стакана")]
        public float GrabScale = 1.5f;

        [Tooltip("Зона захвата не меньше этой доли высоты экрана (радиус): по маленькому стакану легко попасть пальцем")]
        public float GrabMinScreen = 0.08f;

        [Tooltip("Насколько стакан приподнимается, когда над ним мышь, метры сцены")]
        public float HoverLift = 0.06f;

        [Header("Стакан в руке")]
        [Tooltip("Как далеко от точки тряски можно водить стакан, метры сцены")]
        public float Reach = 0.9f;

        [Tooltip("Отступ от бортов лотка: стакан не выходит за стол, метры сцены")]
        public float TrayMargin = 0.45f;

        [Tooltip("Как быстро стакан догоняет палец. Больше = жёстче, меньше = плавнее")]
        public float FollowSharpness = 22f;

        [Tooltip("Наклон стакана в сторону движения: градусы на 1 м/с")]
        public float LeanPerSpeed = 7f;

        [Tooltip("Наибольший наклон стакана при тряске, градусы")]
        public float MaxLean = 28f;

        [Header("Тряска: кости стучат о стенки")]
        [Tooltip("Удар костей слышен при смене направления на такой скорости и выше, м/с")]
        public float HitMinSpeed = 0.5f;

        [Tooltip("При такой скорости удар звучит в полную силу, м/с")]
        public float HitFullSpeed = 3f;

        [Tooltip("Пауза между ударами, секунды: чтобы стук не сливался в гул")]
        public float HitCooldown = 0.06f;

        [Tooltip("Сколько «азарта» добавляет один удар (0..1): от азарта кости сильнее крутятся при броске")]
        public float EnergyPerHit = 0.1f;

        [Tooltip("Азарт гаснет за секунду на столько, если стакан не трясти")]
        public float EnergyDecay = 0.35f;

        [Tooltip("Отдача стакана от удара костей, градусы")]
        public float HitKick = 5f;

        [Tooltip("Трясти камеру от сильных ударов (доля силы удара, 0 = нет)")]
        public float HitCameraShake = 0.012f;

        [Header("Свобода")]
        [Tooltip("Стакан ходит над всем лотком, а не только возле точки тряски (Reach). Выключить — прежнее ограничение")]
        public bool FreeMovement = true;

        [Tooltip("Насколько стакан может выйти за края лотка (для замаха), метры сцены")]
        public float FreeAreaMargin = 0.35f;

        [Tooltip("Бросать в любую сторону — куда взмахнул. Выключить — только к центру лотка, не дальше MaxAngle")]
        public bool FreeDirection = true;

        [Tooltip("Скорость костей, если стакан отпустили без взмаха: они просто высыпаются на месте, м/с")]
        public float DropSpeed = 0.6f;

        [Header("Бросок")]
        [Tooltip("По скольким последним секундам движения считается скорость толчка")]
        public float FlickSampleTime = 0.09f;

        [Tooltip("Скорость толчка к столу, с которой бросок считается броском, м/с. Слабее — стакан просто высыпает кости")]
        public float MinFlickSpeed = 1.2f;

        [Tooltip("Скорость толчка, при которой бросок самый сильный, м/с")]
        public float MaxFlickSpeed = 7f;

        [Tooltip("Скорость вылета костей: от слабого толчка до сильного")]
        public Vector2 ThrowSpeed = new Vector2(1.6f, 4.4f);

        [Tooltip("Скорость вращения костей: от спокойного стакана до хорошо встряхнутого")]
        public Vector2 SpinSpeed = new Vector2(7f, 22f);

        [Tooltip("Насколько направление броска может отклониться от центра лотка, ± градусы")]
        public float MaxAngle = 35f;

        [Tooltip("Длительность рывка с опрокидыванием стакана после отпускания, секунды")]
        public float SwingDuration = 0.12f;

        [Tooltip("Насколько стакан проходит вперёд при рывке, метры сцены")]
        public float SwingDistance = 0.25f;

        [Tooltip("Угол опрокидывания стакана при броске, градусы")]
        public float PourAngle = 110f;

        [Tooltip("Сильный бросок трясёт камеру (0 = нет)")]
        public float StrongThrowCameraShake = 0.03f;

        [Header("Приглашение")]
        [Tooltip("Через сколько секунд ожидания стакан сам покачивается, приглашая взять его. 0 = не покачивается")]
        public float InviteDelay = 3.5f;

        [Tooltip("Пауза между покачиваниями, секунды")]
        public float InviteInterval = 4f;

        [Tooltip("Угол покачивания, градусы")]
        public float InviteAngle = 7f;

        private static ManualRollConfig _fallback;

        /// <summary>Значения по умолчанию, если ассет не задан.</summary>
        public static ManualRollConfig Fallback => _fallback != null ? _fallback : _fallback = CreateInstance<ManualRollConfig>();
    }
}
