using Stateless;
using System;
using UnityEngine;
using Warm.Project.Scripts.Movement;

public class MovementStateMachine
{
    public MovementState CurrentState => _stateMachine.State;

    public event Action<MovementState> StateChanged;

    public event Action OnEnterIdle;
    public event Action OnEnterAcceleration;
    public event Action OnEnterPerformed;
    public event Action OnEnterBraking;

    private StateMachine<MovementState, MovementTrigger> _stateMachine;

    public MovementStateMachine()
    {
        ConfigureStateMachine();
    }

    private void OnIdleEntry()
    {
        OnStateEnter(MovementState.idle);
    }

    private void OnAccelerationEntry()
    {
        OnStateEnter(MovementState.acceleration);
    }

    private void OnPerformedEntry()
    {
        OnStateEnter(MovementState.performed);
    }

    private void OnBrakingEntry()
    {
        OnStateEnter(MovementState.braking);
    }

    private void OnStateEnter(MovementState state)
    {
        switch (state)
        {
            case MovementState.idle:
                OnEnterIdle?.Invoke();
                break;
            case MovementState.acceleration:
                OnEnterAcceleration?.Invoke();
                break;
            case MovementState.performed:
                OnEnterPerformed?.Invoke();
                break;
            case MovementState.braking:
                OnEnterBraking?.Invoke();
                break;
        }

        StateChanged?.Invoke(state);
    }

    private void ConfigureStateMachine()
    {
        _stateMachine = new(MovementState.idle);

        _stateMachine.Configure(MovementState.idle)
            .Permit(MovementTrigger.acceleration, MovementState.acceleration)
            .OnEntry(OnIdleEntry);

        _stateMachine.Configure(MovementState.acceleration)
            .Permit(MovementTrigger.idle, MovementState.idle)
            .Permit(MovementTrigger.performed, MovementState.performed).
            Permit(MovementTrigger.braking, MovementState.braking)
            .OnEntry(OnAccelerationEntry);

        _stateMachine.Configure(MovementState.performed)
            .Permit(MovementTrigger.braking, MovementState.braking)
            .OnEntry(OnPerformedEntry);

        _stateMachine.Configure(MovementState.braking)
            .Permit(MovementTrigger.idle, MovementState.idle)
            .Permit(MovementTrigger.acceleration, MovementState.acceleration)
            .OnEntry(OnBrakingEntry);
    }

    public void FireTrigger(MovementTrigger trigger)
    {
        if (_stateMachine.CanFire(trigger)) _stateMachine.Fire(trigger);
    }

    public void FireIdle()
    {
        FireTrigger(MovementTrigger.idle);
    }

    public void FireAcceleration()
    {
        FireTrigger(MovementTrigger.acceleration);
    }

    public void FirePerformed()
    {
        FireTrigger(MovementTrigger.performed);
    }

    public void FireBraking()
    {
        FireTrigger(MovementTrigger.braking);
    }

    public void UpdateState(float normalizedVelocity, bool isMoving)
    {
        switch (normalizedVelocity, isMoving)
        {
            case (0, false):
                FireIdle();
                break;
            case ( > 0, false):
                FireBraking();
                break;
            case (0, true):
                FireAcceleration();
                break;
            case ( < 1, true):
                FireAcceleration();
                break;
            case (1, true):
                FirePerformed();
                break;
        }
    }
}