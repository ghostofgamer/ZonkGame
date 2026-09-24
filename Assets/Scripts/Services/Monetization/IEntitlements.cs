using System;
using System.Collections.Generic;

namespace Base.Services.Monetization
{
    /// <summary>Права, общие для всех игр.</summary>
    public static class EntitlementIds
    {
        /// <summary>Межстраничная реклама отключена.</summary>
        public const string NoAds = "no_ads";
    }

    /// <summary>
    /// Права игрока: что у него открыто навсегда. Игра спрашивает право, а не покупку:
    /// так одно и то же право можно выдать покупкой, подарком или промокодом.
    /// Хранятся в сохранении, до загрузки сохранения прав нет.
    /// </summary>
    public interface IEntitlements
    {
        bool Has(string entitlement);

        IReadOnlyList<string> All { get; }

        event Action Changed;

        /// <summary>Выдать право и запросить сохранение. Повторная выдача ничего не меняет.</summary>
        void Grant(string entitlement);

        /// <summary>Забрать право. Для возвратов и отладки.</summary>
        void Revoke(string entitlement);
    }
}
