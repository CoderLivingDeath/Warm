namespace Warm.Project.Infrastructure.EventBus
{
    public interface IPlayerInteractionHandler : IGlobalSubscriber
    {
        void HandleInteraction();
    }
}