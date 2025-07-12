using UnityEngine;
using Warm.Project.Infrastructure.EventBus;
using Warm.Project.Infrastructure.Factory;
using Zenject;

[CreateAssetMenu(fileName = "ProjectInstaller", menuName = "Installers/ProjectInstaller")]
public class ProjectInstallerSO : ScriptableObjectInstaller<ProjectInstallerSO>
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<InputSystem_Actions>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<EventBus>().AsSingle().NonLazy();

        Container.BindInterfacesAndSelfTo<InputService>().FromFactory<InputService, CodeGeneratedInputServiceFactory>().AsSingle().NonLazy();
    }
}