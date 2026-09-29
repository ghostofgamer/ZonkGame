using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zonk.UI
{
    /// <summary>Ожидание одного выбора из кнопок окна. После выбора готово к следующему.</summary>
    public sealed class Choice<T>
    {
        private UniTaskCompletionSource<T> _source = new UniTaskCompletionSource<T>();

        public void Set(T value)
        {
            _source.TrySetResult(value);
        }

        public async UniTask<T> WaitAsync(CancellationToken ct)
        {
            var result = await _source.Task.AttachExternalCancellation(ct);
            _source = new UniTaskCompletionSource<T>();
            return result;
        }
    }
}
