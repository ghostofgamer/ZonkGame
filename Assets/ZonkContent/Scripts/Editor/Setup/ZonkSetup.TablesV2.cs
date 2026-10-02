using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Столы, волна 2 (02.10.2026, по отзыву: «тёмный, мраморный, ореховый, берёзовый, золотой — просто цвет меняется»).
    /// Перекраски дубового стола стали своими моделями из zonk_tables.py: ID и цены те же, payload — модель вместо материала.
    /// Плюс новый алхимический стол (нижняя полка с колбами и книгами). Модели v5 доработаны в том же скрипте:
    /// префабы не пересоздаются — ссылка на меш FBX сохраняется при переэкспорте (имя меша то же).
    /// </summary>
    public static partial class ZonkSetup
    {
        private static readonly (string Name, string Id, float Smoothness)[] TablesV2 =
        {
            ("Table_Dark", "table_dark", 0.3f),
            ("Table_Marble", "table_marble", 0.7f),
            ("Table_Walnut", "table_walnut", 0.45f),
            ("Table_Birch", "table_birch", 0.3f),
            ("Table_Gold", "table_gold", 0.65f),
            ("Table_Alchemy", "table_alchemy", 0.3f),
        };

        /// <summary>Цены новых столов волны 2 (перекраски сохраняют свои: монеты, реклама, покупка).</summary>
        private static readonly (string id, Get how, int value)[] TablesV2Layout =
        {
            ("table_alchemy", Get.Play, 25),
        };

        static partial void BuildTablesV2(GameConfig config)
        {
            var tableSlot = SlotAt("table");
            if (tableSlot == null)
                return;

            var order = 30;
            foreach (var (name, id, smoothness) in TablesV2)
            {
                var prefab = ImportedPrefab("Tables", name, smoothness, 0f, LampTextureSize, Color.white);
                if (prefab == null)
                    continue;

                var item = Item(id, tableSlot, order++, new PrefabPayload { Prefab = prefab });
                // Перекраска дубового стола → своя модель; ID, цена и товар площадки не меняются.
                if (item.Payload is MaterialPayload)
                {
                    item.Payload = new PrefabPayload { Prefab = prefab };
                    EditorUtility.SetDirty(item);
                }
            }

            ApplyShopLayoutV6(TablesV2Layout, config);
        }
    }
}
