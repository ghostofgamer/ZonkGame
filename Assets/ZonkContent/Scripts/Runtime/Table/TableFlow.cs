using System;
using System.Collections.Generic;
using System.Threading;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Zonk.Table
{
    /// <summary>ID состояний стола. Новое состояние = класс ITableState и биндинг в TableInstaller.</summary>
    public static class TableStateIds
    {
        public const string Menu = "menu";
        public const string HotSeat = "hotseat";
        public const string Campaign = "campaign";
        public const string Shop = "shop";
        public const string Settings = "settings";
        public const string Rules = "rules";
    }

    /// <summary>
    /// Состояние сцены стола: меню, магазин, партия… Работает, пока не решит, куда дальше,
    /// и возвращает ID следующего состояния.
    /// </summary>
    public interface ITableState
    {
        string Id { get; }
        UniTask<string> RunAsync(CancellationToken ct);
    }

    /// <summary>
    /// Машина состояний стола. Сцена одна: меню, магазин и партия отличаются ракурсом камеры и экраном UI,
    /// поэтому между ними нет загрузок.
    /// </summary>
    public sealed class TableFlow : IInitializable, IDisposable
    {
        private readonly Dictionary<string, ITableState> _states = new Dictionary<string, ITableState>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ISaveStore _saves;

        public TableFlow(List<ITableState> states, ISaveStore saves)
        {
            _saves = saves;
            foreach (var state in states)
                _states[state.Id] = state;
        }

        public void Initialize()
        {
            RunAsync(_cts.Token).Forget();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            // Сцену стола можно запустить в редакторе напрямую, минуя Bootstrap: дождаться сохранения здесь.
            if (await _saves.WaitLoadedAsync(ct).SuppressCancellationThrow())
                return;

            var current = TableStateIds.Menu;
            while (!ct.IsCancellationRequested)
            {
                if (!_states.TryGetValue(current, out var state))
                {
                    Debug.LogError($"[Table] Unknown state '{current}', back to menu");
                    current = TableStateIds.Menu;
                    continue;
                }

                try
                {
                    current = await state.RunAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    // Ошибка в одном состоянии не должна вешать игру: возвращаемся в меню.
                    Debug.LogException(e);
                    current = TableStateIds.Menu;
                }
            }
        }
    }
}
