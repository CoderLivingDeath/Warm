using Cysharp.Threading.Tasks;
using Stateless;
using System;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovementBehaviour : MonoBehaviour
{
    public float _velocity;
    public float _maxVelocity = 0.25f;
    public float _normalizedVelocity;

    public float _directionAngle = 0f; // Угол в градусах, 0 — вправо

    public Vector2 DirectionVector => new Vector2(
        Mathf.Cos(_directionAngle * Mathf.Deg2Rad),
        Mathf.Sin(_directionAngle * Mathf.Deg2Rad)
    );

    public Vector2 _inputMoveVector;
    [Range(0, 20)]
    public float _directionSmoothness = 5f;
    [Range(0, 180)]
    public float oppositeThreshold = 170f;

    public AnimationCurve _accelerationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float _accelerationDuration = 0.25f;

    public AnimationCurve _brakingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float BrakingDuration = 0.25f;

    public PlayerLoopTiming Timing = PlayerLoopTiming.FixedUpdate;

    private MovementStateMachine _stateMachine = new();

    private CancellationTokenSource _velocityCTS;
    private UniTask _velocityTask = UniTask.CompletedTask;

    private CancellationTokenSource _movementCTS;
    private UniTask _movementTask = UniTask.CompletedTask;

    private CancellationTokenSource _directionCts;
    private UniTask _directionTask = UniTask.CompletedTask;

    private Rigidbody2D _rigidbody;

    private async UniTask DirectionUpdateCoroutine(CancellationToken token)
    {
        float lambda = 8f;               // Коэффициент сглаживания
        float oppositeThreshold = 170f;  // Порог (в градусах), при котором сглаживание отключается

        while (!token.IsCancellationRequested)
        {
            float targetAngle = _directionAngle;
            if (_inputMoveVector != Vector2.zero)
            {
                targetAngle = Mathf.Atan2(_inputMoveVector.y, _inputMoveVector.x) * Mathf.Rad2Deg;
            }

            float angleDelta = Mathf.Abs(Mathf.DeltaAngle(_directionAngle, targetAngle));

            if (angleDelta > oppositeThreshold)
            {
                _directionAngle = targetAngle;
            }
            else
            {
                float t = 1f - Mathf.Exp(-lambda * Time.fixedDeltaTime);
                _directionAngle = Mathf.LerpAngle(_directionAngle, targetAngle, t);
            }

            // Ограничение угла от 0 до 360
            _directionAngle = Mathf.Repeat(_directionAngle, 360f);

            await UniTask.Yield(Timing, token);
        }
    }

    private async UniTask MovementCoroutine(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Vector2 offset = DirectionVector * _velocity;
            _rigidbody.MovePosition(_rigidbody.position + offset);

            await UniTask.Yield(Timing, cancellationToken);
        }
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// Асинхронная корутина для расчёта скорости с учётом ускорения или торможения.
    /// При ускорении: velocity растет от 0 до maxVelocity.
    /// При торможении: velocity падает от maxVelocity до 0.
    /// normalizedVelocity всегда в диапазоне [0;1].
    /// </summary>
    public async UniTask VelocityCalculatingCorutin(bool accelerating, CancellationToken cancellationToken)
    {
        AnimationCurve curve = accelerating ? _accelerationCurve : _brakingCurve;
        float totalDuration = accelerating ? _accelerationDuration : BrakingDuration;

        float elapsed = 0f;
        while (!cancellationToken.IsCancellationRequested)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration); // t теперь всегда от 0 до 1

            float curveValue = curve.Evaluate(t);
            _normalizedVelocity = accelerating ? curveValue : 1f - curveValue;
            _velocity = _normalizedVelocity * _maxVelocity;

            await UniTask.Yield(Timing, cancellationToken);
        }
        await UniTask.CompletedTask;
    }

    public void StopDirectionMonitoring()
    {

        if (_directionCts != null)
        {
            _directionCts.Cancel();
            //try
            //{
            //    await _directionTask;
            //}
            //catch (OperationCanceledException)
            //{
            //    Debug.Log("Direction Canceled");
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogError(ex);
            //}
            _directionCts.Dispose();
            _directionCts = null;
        }
    }

    public void StopVelocityCoroutine()
    {
        if (_velocityCTS != null)
        {
            _velocityCTS.Cancel();
            //try
            //{
            //    await _velocityTask;
            //}
            //catch (OperationCanceledException)
            //{
            //    Debug.Log("Velocity Canceled");
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogError(ex);
            //}
            _velocityCTS.Dispose();
            _velocityCTS = null;
        }
    }

    public void StopMovementCoroutine()
    {
        if (_movementCTS != null)
        {
            _movementCTS.Cancel();
            //try
            //{
            //    await _movementTask;
            //}
            //catch (OperationCanceledException)
            //{
            //    Debug.Log("Movement Canceled");
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogError(ex);
            //}
            _movementCTS.Dispose();
            _movementCTS = null;
        }
    }

    public void StartDirectionMonitoring
        (bool restart = true)
    {
        if (_directionCts != null && !restart)
            return; // Уже запущено и не требуется перезапуск

        if (_directionCts != null)
        {
            StopDirectionMonitoring();
        }
        _directionCts = new CancellationTokenSource();
        _directionTask = DirectionUpdateCoroutine(_directionCts.Token);
    }

    public void StartVelocityCoroutineAsync(bool accelerating, bool restart = true)
    {
        if (_velocityCTS != null && !restart)
            return; // Уже запущено и не требуется перезапуск

        if (_velocityCTS != null)
        {
            StopVelocityCoroutine();
        }
        _velocityCTS = new CancellationTokenSource();
        _velocityTask = VelocityCalculatingCorutin(accelerating, _velocityCTS.Token);
    }

    public void StartMovementCoroutineAsync(bool restart = true)
    {
        if (_movementCTS != null && !restart)
            return; // Уже запущено и не требуется перезапуск

        if (_movementCTS != null)
        {
           StopMovementCoroutine();
        }
        _movementCTS = new CancellationTokenSource();
        _movementTask = MovementCoroutine(_movementCTS.Token);
    }

    public bool IsMoving()
    {
        return !_inputMoveVector.Equals(Vector2.zero);
    }

    public void Move(Vector2 direction)
    {
        _inputMoveVector = direction;

        _stateMachine.UpdateState(_normalizedVelocity, IsMoving());
    }

    private void _stateMachine_OnEnterBraking()
    {
        StartVelocityCoroutineAsync(false);
    }

    private void _stateMachine_OnEnterPerformed()
    {
        StopVelocityCoroutine();
    }

    private void _stateMachine_OnEnterAcceleration()
    {
        StartDirectionMonitoring();
        StartVelocityCoroutineAsync(true);
        StartMovementCoroutineAsync();
    }

    private void _stateMachine_OnEnterIdle()
    {
        StopDirectionMonitoring();
        StopVelocityCoroutine();
        StopMovementCoroutine();
    }

    #region Unity Methods
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        _stateMachine.OnEnterIdle += _stateMachine_OnEnterIdle;
        _stateMachine.OnEnterAcceleration += _stateMachine_OnEnterAcceleration;
        _stateMachine.OnEnterPerformed += _stateMachine_OnEnterPerformed;
        _stateMachine.OnEnterBraking += _stateMachine_OnEnterBraking;
    }

    private void OnDisable()
    {
        _stateMachine.OnEnterIdle -= _stateMachine_OnEnterIdle;
        _stateMachine.OnEnterAcceleration -= _stateMachine_OnEnterAcceleration;
        _stateMachine.OnEnterPerformed -= _stateMachine_OnEnterPerformed;
        _stateMachine.OnEnterBraking -= _stateMachine_OnEnterBraking;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + DirectionVector * 2);

        Gizmos.color = Color.azure;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + _inputMoveVector * 2);
    }
    #endregion
}

public class MovementStateMachine
{
    public enum State
    {
        idle, acceleration, performed, braking
    }

    public enum Trigger
    {
        idle, acceleration, performed, braking
    }

    public State CurrentState => _stateMachine.State;

    public event Action<State> StateChanged;

    public event Action OnEnterIdle;
    public event Action OnEnterAcceleration;
    public event Action OnEnterPerformed;
    public event Action OnEnterBraking;

    private StateMachine<State, Trigger> _stateMachine;

    public MovementStateMachine()
    {
        ConfigureStateMachine();
    }

    private void OnIdleEntry()
    {
        OnStateEnter(State.idle);
    }

    private void OnAccelerationEntry()
    {
        OnStateEnter(State.acceleration);
    }

    private void OnPerformedEntry()
    {
        OnStateEnter(State.performed);
    }

    private void OnBrakingEntry()
    {
        OnStateEnter(State.braking);
    }

    private void OnStateEnter(State state)
    {
        switch (state)
        {
            case State.idle:
                OnEnterIdle?.Invoke();
                break;
            case State.acceleration:
                OnEnterAcceleration?.Invoke();
                break;
            case State.performed:
                OnEnterPerformed?.Invoke();
                break;
            case State.braking:
                OnEnterBraking?.Invoke();
                break;
        }

        StateChanged?.Invoke(state);
    }

    private void ConfigureStateMachine()
    {
        _stateMachine = new(State.idle);

        _stateMachine.Configure(State.idle)
            .Permit(Trigger.acceleration, State.acceleration)
            .OnEntry(OnIdleEntry);

        _stateMachine.Configure(State.acceleration)
            .Permit(Trigger.idle, State.idle)
            .Permit(Trigger.performed, State.performed).
            Permit(Trigger.braking, State.braking)
            .OnEntry(OnAccelerationEntry);

        _stateMachine.Configure(State.performed)
            .Permit(Trigger.braking, State.braking)
            .OnEntry(OnPerformedEntry);

        _stateMachine.Configure(State.braking)
            .Permit(Trigger.idle, State.idle)
            .Permit(Trigger.acceleration, State.acceleration)
            .OnEntry(OnBrakingEntry);
    }

    public void FireTrigger(Trigger trigger)
    {
        if (_stateMachine.CanFire(trigger)) _stateMachine.Fire(trigger);
    }

    public void FireIdle()
    {
        FireTrigger(Trigger.idle);
    }

    public void FireAcceleration()
    {
        FireTrigger(Trigger.acceleration);
    }

    public void FirePerformed()
    {
        FireTrigger(Trigger.performed);
    }

    public void FireBraking()
    {
        FireTrigger(Trigger.braking);
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