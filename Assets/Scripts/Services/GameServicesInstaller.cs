using Base.Services.Monetization;
using Base.Services.Saves;
using Zenject;

namespace Base.Services
{
    /// <summary>
    /// Общие сервисы игры поверх площадки: сохранение с версиями, права, покупки, награды,
    /// частота межстраничной рекламы и пауза на время рекламы. Одинаковы для всех игр.
    /// Своё содержимое игра задаёт в MonetizationConfig и своими ISaveMigration.
    /// Вызывается из PlatformInstaller.
    /// </summary>
    public sealed class GameServicesInstaller : Installer<GameServicesInstaller>
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<SaveStore>().AsSingle();

            Container.Bind<MonetizationConfig>().FromInstance(MonetizationConfig.CreateDefault()).AsSingle();
            Container.Bind<IEntitlements>().To<Entitlements>().AsSingle();
            Container.Bind<IPurchaseFlow>().To<PurchaseFlow>().AsSingle();
            Container.Bind<IRewardService>().To<RewardService>().AsSingle();
            Container.BindInterfacesTo<InterstitialService>().AsSingle();
            Container.BindInterfacesTo<AdPauseController>().AsSingle();
        }
    }
}
