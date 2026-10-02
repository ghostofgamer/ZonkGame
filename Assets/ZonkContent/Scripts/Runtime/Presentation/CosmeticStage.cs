using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.Presentation
{
    /// <summary>
    /// Как предмет слота попадает в сцену. Новый способ (например, смена освещения) = новый наследник,
    /// в ассете слота он появится в списке выбора.
    /// </summary>
    [Serializable]
    public abstract class CosmeticApplier
    {
        public abstract void Apply(CosmeticItemConfig item, CosmeticSlotConfig slot, CosmeticStage stage);
    }

    /// <summary>Ставит предмет во все якоря сцены с ID слота. Подходит почти для всего: стол, стакан, лампа, локация.</summary>
    [Serializable]
    public sealed class AnchorPrefabApplier : CosmeticApplier
    {
        public override void Apply(CosmeticItemConfig item, CosmeticSlotConfig slot, CosmeticStage stage)
        {
            stage.ShowInAnchors(slot.Id, item, slot.DefaultItem);
        }
    }

    /// <summary>
    /// Предмет не меняет сцену сразу (стиль броска): он используется в момент действия.
    /// Предпросмотр в магазине показывает ShopWindow.
    /// </summary>
    [Serializable]
    public sealed class NoSceneApplier : CosmeticApplier
    {
        public override void Apply(CosmeticItemConfig item, CosmeticSlotConfig slot, CosmeticStage stage)
        {
        }
    }

    /// <summary>Скин костей: перекрашивает кости игрока на столе.</summary>
    [Serializable]
    public sealed class DiceSkinApplier : CosmeticApplier
    {
        public override void Apply(CosmeticItemConfig item, CosmeticSlotConfig slot, CosmeticStage stage)
        {
            stage.Dice.SetSkin(item);
        }
    }

    /// <summary>
    /// Все места сцены, куда ставится косметика: якоря и кости. Через него надевается экипировка
    /// и показывается примерка в магазине.
    /// </summary>
    public sealed class CosmeticStage : MonoBehaviour
    {
        [SerializeField] private List<CosmeticAnchor> _anchors = new List<CosmeticAnchor>();
        [SerializeField] private DiceSetView _dice;

        private static readonly CosmeticApplier DefaultApplier = new AnchorPrefabApplier();
        private readonly List<SceneLight> _lightBuffer = new List<SceneLight>();

        public DiceSetView Dice => _dice;

        // Дополнительные места слотов (безделушки): что стоит на каждом, решает экипировка игрока; открыто — по таланту.
        private ILoadout _loadout;
        private ITalents _talents;
        private ContentDatabase _content;
        private bool _spotsSubscribed;

        [Inject]
        public void Construct([InjectOptional] ILoadout loadout, [InjectOptional] ITalents talents, [InjectOptional] ContentDatabase content)
        {
            _loadout = loadout;
            _talents = talents;
            _content = content;
            if (isActiveAndEnabled)
                SubscribeSpots();
        }

        private void Start()
        {
            // После внедрения зависимостей во все якоря сцены (им нужны CosmeticAssets для загрузки моделей).
            ApplySpots();
        }

        private void SubscribeSpots()
        {
            if (_spotsSubscribed || _loadout == null)
                return;

            _loadout.Changed += ApplySpots;
            if (_talents != null)
                _talents.Changed += ApplySpots;
            _spotsSubscribed = true;
        }

        private void UnsubscribeSpots()
        {
            if (!_spotsSubscribed)
                return;

            _loadout.Changed -= ApplySpots;
            if (_talents != null)
                _talents.Changed -= ApplySpots;
            _spotsSubscribed = false;
        }

        /// <summary>Расставить вещи по дополнительным местам (якоря с Spot > 0); закрытые места — пустые.</summary>
        public void ApplySpots()
        {
            if (_loadout == null || _content == null)
                return;

            foreach (var anchor in _anchors)
            {
                if (anchor == null || anchor.Spot <= 0)
                    continue;

                var slot = _content.Get<CosmeticSlotConfig>(anchor.SlotId);
                var item = slot != null && anchor.Spot < _loadout.SpotCount(slot) ? _loadout.GetEquippedAt(slot, anchor.Spot) : null;
                anchor.Show(item, null);
            }
        }

        /// <summary>Примерка на месте spot (магазин). Основное место — как Apply.</summary>
        public void ShowAtSpot(CosmeticSlotConfig slot, int spot, CosmeticItemConfig item)
        {
            if (slot == null)
                return;
            if (spot <= 0)
            {
                Apply(slot, item);
                return;
            }

            foreach (var anchor in _anchors)
            {
                if (anchor != null && anchor.SlotId == slot.Id && anchor.Spot == spot)
                    anchor.Show(item, null);
            }
        }

        /// <summary>Якорь места (ракурс магазина смотрит на него); нет — null.</summary>
        public Transform SpotAnchor(string slotId, int spot)
        {
            foreach (var anchor in _anchors)
            {
                if (anchor != null && anchor.SlotId == slotId && anchor.Spot == spot)
                    return anchor.transform;
            }

            return null;
        }

        /// <summary>Предмет надет на стол (экипировка, примерка в магазине, локация главы). Слушает LightingDirector.</summary>
        public event Action<CosmeticItemConfig> ItemApplied;

        /// <summary>
        /// Модель в каком-то якоре сменилась. Префабы грузятся по требованию, поэтому модель появляется позже ItemApplied:
        /// всё, что ищет в моделях (свет ламп и локаций), пересобирается по этому событию.
        /// </summary>
        public event Action InstancesChanged;

        private void OnEnable()
        {
            foreach (var anchor in _anchors)
            {
                if (anchor != null)
                    anchor.InstanceChanged += OnInstanceChanged;
            }
            SubscribeSpots();
        }

        private void OnDisable()
        {
            foreach (var anchor in _anchors)
            {
                if (anchor != null)
                    anchor.InstanceChanged -= OnInstanceChanged;
            }
            UnsubscribeSpots();
        }

        private void OnInstanceChanged(CosmeticAnchor anchor)
        {
            InstancesChanged?.Invoke();
        }

        /// <summary>Дождаться, пока все якоря поставят последние показанные модели (префабы загрузятся).</summary>
        public async UniTask WhenReadyAsync(CancellationToken ct)
        {
            foreach (var anchor in _anchors)
            {
                if (anchor != null)
                    await anchor.WhenReadyAsync().AttachExternalCancellation(ct);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(List<CosmeticAnchor> anchors, DiceSetView dice)
        {
            _anchors = anchors;
            _dice = dice;
        }
#endif

        /// <summary>Поставить предмет во все якоря слота slotId (основное место; дополнительные — ApplySpots).</summary>
        public void ShowInAnchors(string slotId, CosmeticItemConfig item, CosmeticItemConfig slotDefault)
        {
            foreach (var anchor in _anchors)
            {
                if (anchor != null && anchor.SlotId == slotId && anchor.Spot == 0)
                    anchor.Show(item, slotDefault);
            }
        }

        public void Apply(CosmeticItemConfig item)
        {
            if (item == null || item.Slot == null)
                return;

            var applier = item.Slot.Applier ?? DefaultApplier;
            applier.Apply(item, item.Slot, this);
            ItemApplied?.Invoke(item);
        }

        /// <summary>Показать предмет слота; null = базовый предмет слота.</summary>
        public void Apply(CosmeticSlotConfig slot, CosmeticItemConfig item)
        {
            if (slot == null)
                return;

            // Слот с местами: основное место может быть пустым (вещь переставили) — без базового предмета.
            if (item == null && slot.ExtraSpots > 0)
            {
                ShowInAnchors(slot.Id, null, null);
                return;
            }

            item = item != null ? item : slot.DefaultItem;
            var applier = slot.Applier ?? DefaultApplier;
            if (item != null)
            {
                applier.Apply(item, slot, this);
                ItemApplied?.Invoke(item);
            }
        }

        /// <summary>Все источники света (SceneLight) в предметах на столе: лампа, локация. Вызывается при смене предметов.</summary>
        public void CollectLights(List<SceneLight> result)
        {
            result.Clear();
            foreach (var anchor in _anchors)
            {
                if (anchor == null || anchor.Instance == null)
                    continue;

                anchor.Instance.GetComponentsInChildren(true, _lightBuffer);
                result.AddRange(_lightBuffer);
            }
        }
    }
}
