using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Zonk.Configs;
using Zonk.Utils;

namespace Zonk.UI.Transitions
{
    /// <summary>
    /// Стиль появления или скрытия окна: набор эффектов (прозрачность, прилёт, масштаб, поворот, по очереди…),
    /// которые играют одновременно. Окну назначаются два таких ассета: на показ и на скрытие.
    /// Готовые стили создаёт генератор в Configs/Ui/Transitions, новые собираются в инспекторе без кода.
    /// Играет в реальном времени (игнорирует Time.timeScale), чтобы окна работали и на паузе.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/UI/Transition", fileName = "Transition")]
    public sealed class UiTransitionConfig : ScriptableObject
    {
        [Min(0f)] public float Duration = 0.3f;
        public Ease Ease = Ease.OutCubic;

        [SerializeReference, SubclassSelector]
        public List<UiEffect> Effects = new List<UiEffect>();

        public Sequence Build(UiTransitionTarget target)
        {
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(target.Rect.gameObject);
            foreach (var effect in Effects)
                effect?.AddTo(sequence, target, Duration, Ease);
            return sequence;
        }

        public UniTask PlayAsync(UiTransitionTarget target, CancellationToken ct)
        {
            return Build(target).AwaitAsync(ct);
        }
    }
}
