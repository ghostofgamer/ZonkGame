using Base.Core.Localization;
using Base.Services;
using Base.Services.Quality;
using Base.Platform.Stub;
using Zenject;

namespace Base.Platform.Installers
{
    /// <summary>
    /// Единственное место, где выбирается реализация площадки.
    /// В редакторе всегда Stub: SDK площадок работают только в реальном билде.
    /// В билде реализация выбирается по define BASE_YANDEX / BASE_VKPLAY / BASE_RUSTORE / BASE_VKGAMES,
    /// без define используется Stub.
    /// </summary>
    public sealed class PlatformInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
#if UNITY_EDITOR || !(BASE_YANDEX || BASE_VKPLAY || BASE_RUSTORE || BASE_VKGAMES)
            Container.Bind<IPlatformService>().To<StubPlatformService>().AsSingle();
            Container.Bind<IAdsService>().To<StubAdsService>().AsSingle();
            Container.Bind<IPurchaseService>().To<StubPurchaseService>().AsSingle();
            Container.Bind<ICloudSaveService>().To<StubCloudSaveService>().AsSingle();
            Container.Bind<ILeaderboardService>().To<StubLeaderboardService>().AsSingle();
#elif BASE_YANDEX
            Container.Bind<IPlatformService>().To<Yandex.YandexPlatformService>().AsSingle();
            Container.Bind<IAdsService>().To<Yandex.YandexAdsService>().AsSingle();
            Container.Bind<IPurchaseService>().To<Yandex.YandexPurchaseService>().AsSingle();
            Container.Bind<ICloudSaveService>().To<Yandex.YandexCloudSaveService>().AsSingle();
            Container.Bind<ILeaderboardService>().To<Yandex.YandexLeaderboardService>().AsSingle();
#elif BASE_VKPLAY
            Container.Bind<IPlatformService>().To<VKPlay.VKPlayPlatformService>().AsSingle();
            Container.Bind<IAdsService>().To<VKPlay.VKPlayAdsService>().AsSingle();
            Container.Bind<IPurchaseService>().To<VKPlay.VKPlayPurchaseService>().AsSingle();
            Container.Bind<ICloudSaveService>().To<VKPlay.VKPlayCloudSaveService>().AsSingle();
            Container.Bind<ILeaderboardService>().To<VKPlay.VKPlayLeaderboardService>().AsSingle();
#elif BASE_RUSTORE
            Container.Bind<IPlatformService>().To<RuStore.RuStorePlatformService>().AsSingle();
            Container.Bind<IAdsService>().To<RuStore.RuStoreAdsService>().AsSingle();
            Container.Bind<IPurchaseService>().To<RuStore.RuStorePurchaseService>().AsSingle();
            Container.Bind<ICloudSaveService>().To<RuStore.RuStoreCloudSaveService>().AsSingle();
            Container.Bind<ILeaderboardService>().To<RuStore.RuStoreLeaderboardService>().AsSingle();
#elif BASE_VKGAMES
            Container.Bind<IPlatformService>().To<VKGames.VKGamesPlatformService>().AsSingle();
            Container.Bind<IAdsService>().To<VKGames.VKGamesAdsService>().AsSingle();
            Container.Bind<IPurchaseService>().To<VKGames.VKGamesPurchaseService>().AsSingle();
            Container.Bind<ICloudSaveService>().To<VKGames.VKGamesCloudSaveService>().AsSingle();
            Container.Bind<ILeaderboardService>().To<VKGames.VKGamesLeaderboardService>().AsSingle();
#endif

            // Язык интерфейса берётся из SDK площадки, см. PlatformInitializer.
            Container.Bind<ILocalization>().To<Localization>().AsSingle();

            // Качество графики выбирается по типу устройства от площадки, см. PlatformInitializer.
            Container.Bind<IQualityService>().To<QualityService>().AsSingle();

            // Сохранение, права, покупки, награды и правила рекламы: общий слой игры поверх площадки.
            GameServicesInstaller.Install(Container);

            Container.BindInterfacesTo<PlatformInitializer>().AsSingle();
        }
    }
}
