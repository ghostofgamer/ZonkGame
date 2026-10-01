using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;

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
            foreach (var anchor in stage.AnchorsFor(slot.Id))
                anchor.Show(item, slot.DefaultItem);
        }
    }


    /// <summary>
    /// Предмет не меняет сцену сразу (стиль броска): он используется в момент действия.
    /// Предпросмотр в магазине показывает ShopScreen.
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

        public DiceSetView Dice => _dice;

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
        }

        private void OnDisable()
        {
            foreach (var anchor in _anchors)
            {
                if (anchor != null)
                    anchor.InstanceChanged -= OnInstanceChanged;
            }
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

        public IEnumerable<CosmeticAnchor> AnchorsFor(string slotId)
        {
            foreach (var anchor in _anchors)
            {
                if (anchor != null && anchor.SlotId == slotId)
                    yield return anchor;
            }
        }

        public void Apply(CosmeticItemConfig item)
        {
            if (item == null || item.Slot == null)
                return;

            var applier = item.Slot.Applier ?? new AnchorPrefabApplier();
            applier.Apply(item, item.Slot, this);
            ItemApplied?.Invoke(item);
        }

        /// <summary>Показать предмет слота; null = базовый предмет слота.</summary>
        public void Apply(CosmeticSlotConfig slot, CosmeticItemConfig item)
        {
            if (slot == null)
                return;

            item = item != null ? item : slot.DefaultItem;
            var applier = slot.Applier ?? new AnchorPrefabApplier();
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
                if (anchor != null && anchor.Instance != null)
                    result.AddRange(anchor.Instance.GetComponentsInChildren<SceneLight>(true));
            }
        }
    }
}
