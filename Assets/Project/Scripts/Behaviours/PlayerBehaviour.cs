using UnityEngine;
using Warm.Project.Infrastructure.EventBus;
using Zenject;

[RequireComponent(typeof(MovementBehaviour))]
public class PlayerBehaviour : MonoBehaviour, IPlayerMovementHandler
{
    private MovementBehaviour _movementBehaviour;

    [Inject]
    private EventBus _eventBus;

    #region Event Handlers

    public void HandleMovement(Vector2 direction)
    {
        _movementBehaviour.Move(direction);
    }

    #endregion

    #region Unity Methods

    void Start()
    {
        _movementBehaviour = GetComponent<MovementBehaviour>();
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