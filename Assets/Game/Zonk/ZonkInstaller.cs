using Zenject;

namespace Zonk
{
    /// <summary>
    /// Сервисы игры, которые живут всё время, и миграции сохранений (ISaveMigration).
    /// Подключён в Assets/Resources/ProjectContext.prefab после PlatformInstaller.
    /// Сервисы одной сцены биндятся в SceneContext этой сцены.
    /// </summary>
    public sealed class ZonkInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
        }
    }
}
