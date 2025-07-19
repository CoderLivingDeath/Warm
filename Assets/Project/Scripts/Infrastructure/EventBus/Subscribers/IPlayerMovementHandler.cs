using UnityEngine;
namespace Warm.Project.Infrastructure.EventBus
{
    public interface IPlayerMovementHandler : IGlobalSubscriber
    {
        void HandleMovement(Vector2 direction);
    }
}
