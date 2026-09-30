using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Ощущение партии: всплывающие очки и названия комбинаций, накрутка счёта, замедление на решающей кости,
    /// искры и пыль, вибрация. Только показ: на правила и результат не влияет.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Match Feel", fileName = "MatchFeel")]
    public sealed class MatchFeelConfig : ScriptableObject
    {
        [Header("Очки над отложенными костями")]
        [Tooltip("Размер шрифта «+350»")]
        public float PopupFontSize = 72f;

        [Tooltip("Размер шрифта названия комбинации («Стрит», «Три шестёрки»)")]
        public float ComboFontSize = 44f;

        [Tooltip("Сколько секунд видна надпись")]
        public float PopupDuration = 1.1f;

        [Tooltip("На сколько пикселей холста надпись всплывает вверх")]
        public float PopupRise = 110f;

        [Tooltip("Надпись над костями выше центра костей, метры сцены. Крупная комбинация (GameConfig.BigKeepScore) — " +
                 "надпись крупнее, с искрами")]
        public float PopupHeight = 0.35f;

        [Header("Кости без очков при выборе")]
        [Tooltip("Яркость костей, которые не входят ни в одну комбинацию (1 = как есть)")]
        public float UnusedDiceBrightness = 0.45f;

        [Tooltip("Насколько обесцвечивать такие кости (0..1): на цветных скинах одного затемнения мало")]
        public float UnusedDiceDesaturate = 0.85f;

        [Header("Смена игрока в игре вдвоём")]
        [Tooltip("Длительность смены: стаканы по дуге меняются местами, ближняя рука уходит и возвращается рукой нового игрока, секунды")]
        public float SeatSwapDuration = 0.6f;

        [Tooltip("Частиц пыли под каждым стаканом при приземлении")]
        public int SeatSwapDust = 10;

        [Header("Накрутка счёта")]
        [Tooltip("Очки хода накручиваются за столько секунд")]
        public float TurnCountDuration = 0.45f;

        [Tooltip("Счёт игрока после «Забрать» накручивается за столько секунд")]
        public float BankCountDuration = 0.8f;

        [Tooltip("Насколько подпрыгивает число при росте (масштаб)")]
        public float CountPunch = 1.2f;

        [Tooltip("Длительность подпрыгивания, секунды")]
        public float CountPunchDuration = 0.25f;

        [Header("Замедление на решающей кости")]
        [Tooltip("Замедлять, если брошено не больше стольких костей (самые рискованные броски). 0 = не замедлять")]
        public int SlowMoMaxDice = 2;

        [Tooltip("Скорость показа в замедлении (1 = обычная)")]
        public float SlowMoFactor = 0.35f;

        [Tooltip("Замедляется не больше стольких последних секунд качения, секунды записи")]
        public float SlowMoWindow = 0.7f;

        [Tooltip("Замедлять, только если последняя кость катится после остальных хотя бы столько, секунды записи")]
        public float SlowMoMinGap = 0.2f;

        [Header("Пыль от ударов костей о стол")]
        public ParticleSystem DustPrefab;

        [Tooltip("Пыль только от удара с такой скоростью падения и выше, м/с")]
        public float DustMinSpeed = 1.2f;

        [Tooltip("Частиц пыли: от слабого удара до сильного")]
        public Vector2Int DustCount = new Vector2Int(3, 10);

        [Tooltip("При такой скорости падения удар считается самым сильным, м/с")]
        public float DustFullSpeed = 4f;

        [Header("Искры: горячие кости и крупные комбинации")]
        public ParticleSystem SparksPrefab;

        [Tooltip("Искр на каждую кость при горячих костях")]
        public int HotDiceSparks = 14;

        [Tooltip("Искр на каждую кость при крупной комбинации")]
        public int BigKeepSparks = 6;

        [Tooltip("Толчок камеры при горячих костях: сила и длительность")]
        public Vector2 HotDiceCameraShake = new Vector2(0.1f, 0.35f);

        [Header("Кости внутри стакана")]
        [Tooltip("Показывать кости внутри взятого стакана: они болтаются от движений стакана")]
        public bool ShowDiceInCup = true;

        [Tooltip("Размер костей в стакане — доля обычного: в стакане кости меньше, чтобы свободно болтались и не " +
                 "торчали сквозь стенки. На столе размер обычный")]
        public float CupDiceScale = 0.6f;

        [Tooltip("Какая доля ускорения стакана передаётся костям: больше — кости сильнее отстают от стакана и бьются о стенки")]
        public float CupInertiaShare = 0.3f;

        [Tooltip("Подброс костей от каждого стука при ручной тряске, м/с (при самом сильном ударе)")]
        public float CupHitKick = 2.5f;

        [Tooltip("Наибольшее ускорение костей в стакане, м/с²: чтобы не пробивали стенки на рывках")]
        public float CupMaxAcceleration = 70f;

        [Tooltip("Сила тяжести внутри стакана, м/с²")]
        public float CupGravity = 9.81f;

        [Tooltip("Упругость ударов о стенки и дно (0 — не отскакивают, 1 — без потерь)")]
        public float CupBounce = 0.45f;

        [Tooltip("Трение о стенки и дно: какая доля скольжения теряется за удар")]
        public float CupFriction = 0.25f;

        [Tooltip("Закрутка кости от удара о стенку, радиан/с на 1 м/с удара")]
        public float CupSpinPerHit = 9f;

        [Tooltip("Затухание вращения костей в стакане за секунду")]
        public float CupSpinDamping = 2.5f;

        [Tooltip("Внутренний радиус стакана — доля внешнего (толщина стенок)")]
        public float CupInnerRadius = 0.88f;

        [Tooltip("Высота внутреннего дна — доля высоты стакана")]
        public float CupFloor = 0.05f;

        [Header("Вибрация (Android), миллисекунды")]
        [Tooltip("Не чаще, чем раз в столько секунд")]
        public float HapticMinInterval = 0.04f;

        public int HapticCupHit = 12;
        public int HapticDiceLand = 8;
        public int HapticThrow = 25;
        public int HapticKeep = 15;
        public int HapticHotDice = 45;
        public int HapticZonk = 90;
        public int HapticBank = 30;

        private static MatchFeelConfig _fallback;

        /// <summary>Значения по умолчанию, если ассет не задан.</summary>
        public static MatchFeelConfig Fallback => _fallback != null ? _fallback : _fallback = CreateInstance<MatchFeelConfig>();
    }
}
