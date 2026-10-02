using UnityEngine;
using Zenject;
using Zonk.MatchFlow;
using Zonk.Presentation;
using Zonk.UI;

namespace Zonk.Table
{
    /// <summary>
    /// Сервисы сцены стола. Новое состояние стола (например, онлайн-лобби) = класс ITableState
    /// и одна строка здесь.
    /// </summary>
    public sealed class TableInstaller : MonoInstaller
    {
        [SerializeField] private TableView _table;

#if UNITY_EDITOR
        public void EditorSetup(TableView table)
        {
            _table = table;
        }
#endif

        public override void InstallBindings()
        {
            Container.Bind<TableView>().FromInstance(_table).AsSingle();
            Container.Bind<UiKit>().AsSingle();
            Container.Bind<RectTransform>().WithId(UiService.UiRootId).FromInstance(_table.UiRoot);
            Container.BindInterfacesTo<UiService>().AsSingle();

            Container.BindInterfacesAndSelfTo<MatchPresenter>().AsSingle();
            Container.Bind<ReactionDirector>().AsSingle();
            Container.Bind<IChatChannel>().To<LocalChatChannel>().AsSingle();
            Container.Bind<MatchRunner>().AsSingle();
            Container.Bind<MatchAftermath>().AsSingle();

            Container.Bind<ParticipantFactory>().AsSingle();
            Container.Bind<StageDresser>().AsSingle();
            Container.Bind<OwnedContent>().AsSingle();
            Container.Bind<ShopFocus>().AsSingle();
            Container.BindInterfacesAndSelfTo<TutorialDirector>().AsSingle();
            Container.Bind<EnergyGate>().AsSingle();
            Container.Bind<ModeMatch>().AsSingle();

            Container.Bind<ITableState>().To<MenuState>().AsSingle();
            Container.Bind<ITableState>().To<SettingsState>().AsSingle();
            Container.Bind<ITableState>().To<ShopState>().AsSingle();
            Container.Bind<ITableState>().To<HotSeatState>().AsSingle();
            Container.Bind<ITableState>().To<CampaignState>().AsSingle();
            Container.Bind<ITableState>().To<RulesState>().AsSingle();
            Container.Bind<ITableState>().To<QuestsState>().AsSingle();
            Container.Bind<ITableState>().To<LeaderboardsState>().AsSingle();
            Container.Bind<ITableState>().To<TowerState>().AsSingle();
            Container.Bind<ITableState>().To<EndlessRunState>().AsSingle();
            Container.Bind<ITableState>().To<ProfileState>().AsSingle();
            Container.Bind<ITableState>().To<ChestState>().AsSingle();
            Container.Bind<ITableState>().To<SeasonState>().AsSingle();

            Container.BindInterfacesTo<TableFlow>().AsSingle();
        }
    }
}
