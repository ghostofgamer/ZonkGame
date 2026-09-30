using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Все объекты сцены стола в одном месте. Ссылки проставляет генератор сцены (Zonk/Setup),
    /// TableInstaller отдаёт их в контейнер. Поиск объектов по сцене не используется.
    /// </summary>
    public sealed class TableView : MonoBehaviour
    {
        [SerializeField] private CameraRig _camera;
        [SerializeField] private DiceTrayView _tray;
        [SerializeField] private DiceSetView _dice;
        [SerializeField] private SeatView _south;
        [SerializeField] private SeatView _north;
        [SerializeField] private OpponentAvatarView _opponent;
        [SerializeField] private CosmeticStage _stage;
        [SerializeField] private SoundPlayer _sound;
        [SerializeField] private RectTransform _uiRoot;

        public CameraRig Camera => _camera;
        public DiceTrayView Tray => _tray;
        public DiceSetView Dice => _dice;
        public SeatView South => _south;
        public SeatView North => _north;
        public OpponentAvatarView Opponent => _opponent;
        public CosmeticStage Stage => _stage;
        public SoundPlayer Sound => _sound;
        public RectTransform UiRoot => _uiRoot;

        /// <summary>
        /// Кто сидит на юге, у камеры. Обычно первый игрок; в игре вдвоём на одном устройстве — тот, чей ход
        /// (MatchPresenter меняет места в начале хода).
        /// </summary>
        public int NearPlayer { get; set; }

        /// <summary>Место игрока: ближний (NearPlayer) сидит на юге, у камеры, другой — на севере.</summary>
        public SeatView SeatOf(int player)
        {
            return player == NearPlayer ? _south : _north;
        }

#if UNITY_EDITOR
        public void EditorSetup(CameraRig camera, DiceTrayView tray, DiceSetView dice, SeatView south, SeatView north,
            OpponentAvatarView opponent, CosmeticStage stage, SoundPlayer sound, RectTransform uiRoot)
        {
            _camera = camera;
            _tray = tray;
            _dice = dice;
            _south = south;
            _north = north;
            _opponent = opponent;
            _stage = stage;
            _sound = sound;
            _uiRoot = uiRoot;
        }
#endif
    }
}
