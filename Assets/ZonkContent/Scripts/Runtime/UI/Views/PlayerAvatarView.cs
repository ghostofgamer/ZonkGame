using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.Table;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Облик игрока (деталь Parts/PlayerAvatar): аватар и рамка поверх него. Картинки — SpritePayload предметов
    /// слотов «avatar» и «frame». Без ShowItems показывает надетое у игрока и обновляется при смене.
    /// Кнопка на детали (если есть) сообщает Clicked: в меню открывает профиль.
    /// </summary>
    public sealed class PlayerAvatarView : MonoBehaviour
    {
        [SerializeField] private Image _avatar;
        [SerializeField] private Image _frame;

        private ILoadout _loadout;
        private ContentDatabase _content;
        private bool _showsEquipped = true;

        public event Action Clicked;

        [Inject]
        public void Construct([InjectOptional] ILoadout loadout, [InjectOptional] ContentDatabase content)
        {
            _loadout = loadout;
            _content = content;
        }

#if UNITY_EDITOR
        public void EditorSetup(Image avatar, Image frame)
        {
            _avatar = avatar;
            _frame = frame;
        }
#endif

        private void Awake()
        {
            var button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() => Clicked?.Invoke());
        }

        private void OnEnable()
        {
            if (_loadout != null)
                _loadout.Changed += OnLoadoutChanged;
            if (_showsEquipped)
                ShowEquipped();
        }

        private void OnDisable()
        {
            if (_loadout != null)
                _loadout.Changed -= OnLoadoutChanged;
        }

        /// <summary>Надетое у игрока (по умолчанию).</summary>
        public void ShowEquipped()
        {
            _showsEquipped = true;
            if (_loadout == null || _content == null)
                return;

            Paint(Equipped(SlotIds.Avatar), Equipped(SlotIds.Frame));
        }

        /// <summary>Конкретные предметы (примерка, соперник). null — пусто.</summary>
        public void ShowItems(CosmeticItemConfig avatar, CosmeticItemConfig frame)
        {
            _showsEquipped = false;
            Paint(avatar, frame);
        }

        private void OnLoadoutChanged()
        {
            if (_showsEquipped)
                ShowEquipped();
        }

        private CosmeticItemConfig Equipped(string slotId)
        {
            var slot = _content.Get<CosmeticSlotConfig>(slotId);
            return slot != null ? _loadout.GetEquipped(slot) : null;
        }

        private void Paint(CosmeticItemConfig avatar, CosmeticItemConfig frame)
        {
            SetSprite(_avatar, SpriteOf(avatar));
            SetSprite(_frame, SpriteOf(frame));
        }

        public static Sprite SpriteOf(CosmeticItemConfig item)
        {
            return item != null && item.Payload is SpritePayload payload ? payload.Sprite : null;
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
