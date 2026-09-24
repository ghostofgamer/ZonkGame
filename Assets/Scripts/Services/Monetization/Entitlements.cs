using System;
using System.Collections.Generic;
using Base.Services.Saves;
using UnityEngine;

namespace Base.Services.Monetization
{
    /// <summary>Права игрока в разделе сохранения "entitlements".</summary>
    public sealed class Entitlements : IEntitlements
    {
        private const string SaveKey = "entitlements";
        private static readonly IReadOnlyList<string> None = new string[0];

        private readonly ISaveStore _saves;

        public Entitlements(ISaveStore saves)
        {
            _saves = saves;
        }

        public event Action Changed;

        public IReadOnlyList<string> All => _saves.IsLoaded ? Owned : None;

        private List<string> Owned
        {
            get
            {
                var data = _saves.Get<EntitlementsData>(SaveKey);
                return data.owned ?? (data.owned = new List<string>());
            }
        }

        public bool Has(string entitlement)
        {
            return _saves.IsLoaded && Owned.Contains(entitlement);
        }

        public void Grant(string entitlement)
        {
            if (Owned.Contains(entitlement))
                return;

            Owned.Add(entitlement);
            Debug.Log($"[Purchases] Entitlement granted: {entitlement}");
            _saves.RequestSave();
            Changed?.Invoke();
        }

        public void Revoke(string entitlement)
        {
            if (!Owned.Remove(entitlement))
                return;

            Debug.Log($"[Purchases] Entitlement revoked: {entitlement}");
            _saves.RequestSave();
            Changed?.Invoke();
        }

        [Serializable]
        internal sealed class EntitlementsData
        {
            public List<string> owned = new List<string>();
        }
    }
}
