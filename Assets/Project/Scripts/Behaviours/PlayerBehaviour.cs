using UnityEngine;
using Warm.Project.Infrastructure.EventBus;
using Zenject;

[RequireComponent(typeof(MovementBehaviour), typeof(InteractionBehaviour))]
public class PlayerBehaviour : MonoBehaviour, IPlayerMovementHandler, IPlayerInteractionHandler
{
    private MovementBehaviour _movementBehaviour;
    private InteractionBehaviour _interactionBehaviour;

    [Inject]
    private EventBus _eventBus;

    #region Event Handlers

    public void HandleMovement(Vector2 direction)
    {
        _movementBehaviour.Move(direction);
    }
    public void HandleInteraction()
    {
        _interactionBehaviour.Interact();
    }

    #endregion

    #region Unity Methods

    void Start()
    {
        _movementBehaviour = GetComponent<MovementBehaviour>();
        _interactionBehaviour = GetComponent<InteractionBehaviour>();
    }

    private void OnEnable()
    {
        _eventBus.Subscribe(this);
    }

    private void OnDisable()
    {
        _eventBus.Unsubscribe(this);
    }

    #endregion
}