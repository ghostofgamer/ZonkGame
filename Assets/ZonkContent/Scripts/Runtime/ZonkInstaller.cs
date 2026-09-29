using System.Collections.Generic;
using Base.Core.Localization;
using Base.Services.Monetization;
using Base.Services.Texts;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk
{
    /// <summary>
    /// Сервисы игры, которые живут всё время, и миграции сохранений (ISaveMigration).
    /// Подключён в Assets/Resources/ProjectContext.prefab после PlatformInstaller.
    /// Сервисы одной сцены биндятся в SceneContext этой сцены (TableInstaller).
    /// Ссылки на ассеты проставляет меню Zonk/Setup/Build Everything.
    /// </summary>
    public sealed class ZonkInstaller : MonoInstaller
    {
        [SerializeField] private ContentDatabase _content;
        [SerializeField] private GameConfig _config;

        [Tooltip("CSV с текстами игры (key, ru, en), скачивается из Google Таблицы: Base/Localization/Download Sheets")]
        [SerializeField] private TextAsset _texts;

#if UNITY_EDITOR
        public void EditorSetup(ContentDatabase content, GameConfig config, TextAsset texts)
        {
            _content = content;
            _config = config;
            _texts = texts;
        }
#endif

        public override void InstallBindings()
        {
            Container.Bind<Zonk.UI.LoadingScreenHolder>().AsSingle();

            // До первого запуска генератора ссылок нет: игра не собрана, но тестовая сцена площадок должна работать.
            if (_content == null || _config == null)
            {
                Debug.LogError("[Zonk] ZonkInstaller: content database or game config is not set. Run Zonk/Setup/Build Everything");
                return;
            }

            Container.Bind<ContentDatabase>().FromInstance(_content).AsSingle();
            Container.Bind<GameConfig>().FromInstance(_config).AsSingle();

            if (_texts != null)
                Container.Bind<ILocalizationSource>().FromInstance(new TextAssetLocalizationSource(_texts));

            BindMonetization();

            Container.Bind<IWallet>().To<Wallet>().AsSingle();
            Container.Bind<IInventory>().To<Inventory>().AsSingle();
            Container.Bind<ILoadout>().To<Loadout>().AsSingle();
            Container.Bind<ICampaignProgress>().To<CampaignProgress>().AsSingle();
            Container.Bind<IGameSettings>().To<GameSettings>().AsSingle();
            Container.Bind<HotSeatSettings>().AsSingle();
            Container.Bind<RewardGranter>().AsSingle();
            Container.Bind<ShopService>().AsSingle();
        }

        /// <summary>
        /// Товары за реальные деньги берутся из контента: каждый PurchasePriceOption — постоянный товар,
        /// его ID совпадает с ID в консоли площадки. Плюс товары шаблона (no_ads).
        /// </summary>
        private void BindMonetization()
        {
            var config = MonetizationConfig.CreateDefault();
            var products = new List<ProductDefinition>(config.Products);
            var known = new HashSet<string>();
            foreach (var product in products)
                known.Add(product.Id);

            if (_content != null)
            {
                foreach (var item in _content.Items)
                {
                    var price = Pricing.PriceOf(item);
                    if (price == null)
                        continue;

                    foreach (var option in price.Options)
                    {
                        if (option is PurchasePriceOption purchase && !string.IsNullOrEmpty(purchase.ProductId) &&
                            known.Add(purchase.ProductId))
                        {
                            products.Add(new ProductDefinition(purchase.ProductId, ProductKind.Permanent));
                        }
                    }
                }
            }

            config.Products = products;
            // Без AsSingle: шаблон уже пометил тип синглтоном, и Zenject 6+ запрещает второй AsSingle даже после Unbind.
            // Экземпляр один и тот же, поэтому FromInstance достаточно.
            Container.Rebind<MonetizationConfig>().FromInstance(config);
        }
    }
}
