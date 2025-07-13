using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Warm.Project.Scripts.Movement;

[RequireComponent(typeof(Rigidbody2D))]
public class MovementBehaviour : MonoBehaviour
{
    public MovementState CurrentState => _stateMachine.CurrentState;
    [SerializeField]
    private MovementState _currentState;

    public Vector2 Position => _rigidbody.position;

    public float Velocity => _velocity;
    [SerializeField]
    private float _velocity;

    public float MaxVelocity => _maxVelocity;
    [SerializeField]
    private float _maxVelocity = 0.25f;

    public float NormalizedVelocity => _normalizedVelocity;
    [SerializeField]
    private float _normalizedVelocity;

    public float DirectionAngle => _directionAngle;
    [SerializeField]
    private float _directionAngle = 0f;

    public Vector2 DirectionVector => new Vector2(
        Mathf.Cos(_directionAngle * Mathf.Deg2Rad),
        Mathf.Sin(_directionAngle * Mathf.Deg2Rad)
    );


    [Range(0, 20)]
    public float _directionSmoothness = 5f;
    [Range(0, 180)]
    public float oppositeThreshold = 170f;

    public AnimationCurve _accelerationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float _accelerationDuration = 0.25f;

    public AnimationCurve _brakingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float BrakingDuration = 0.25f;

    private float _currentDuration;

    public PlayerLoopTiming Timing = PlayerLoopTiming.FixedUpdate;

    // Events

    public event Action PositionChanged;

    public event Action<MovementState> StateChanged;

    private MovementStateMachine _stateMachine = new();

    private Rigidbody2D _rigidbody;

    [SerializeField]
    private Vector2 _inputMoveVector;

    // Tasks
    private CancellationTokenSource _velocityCTS;
    private UniTask _velocityTask = UniTask.CompletedTask;

    private CancellationTokenSource _movementCTS;
    private UniTask _movementTask = UniTask.CompletedTask;

    private CancellationTokenSource _directionCts;
    private UniTask _directionTask = UniTask.CompletedTask;

    private CancellationTokenSource _stateMonitoringCTS;
    private UniTask _stateMonitoringTask = UniTask.CompletedTask;

    private CancellationTokenSource _AccelerationCTS;
    private UniTask _accelerationTask = UniTask.CompletedTask;

    private CancellationTokenSource _brakingCTS;
    private UniTask _brakingTask = UniTask.CompletedTask;

    public async UniTask StateMonitoringTask(CancellationToken cancellationToken)
    {
        int GetVelocityCategory(float velocity)
        {
            if (velocity == 0f)
                return 0;
            if (velocity == 1f)
                return 2;
            return 1; // velocity >0 и <1
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            int prevCategory = GetVelocityCategory(NormalizedVelocity);
            bool prevIsMoving = IsMoving();

            await UniTask.WaitUntil(() =>
            {
                int currentCategory = GetVelocityCategory(NormalizedVelocity);
                bool currentIsMoving = IsMoving();

                return currentCategory != prevCategory || currentIsMoving != prevIsMoving;

            }, Timing, cancellationToken);

            _stateMachine.UpdateState(_normalizedVelocity, IsMoving());

            _currentState = CurrentState;
            prevCategory = GetVelocityCategory(NormalizedVelocity);
            prevIsMoving = IsMoving();

            await UniTask.Yield(Timing, cancellationToken);
        }
    }

    private async UniTask DirectionUpdateTask(CancellationToken token)
    {
        _directionAngle = _inputMoveVector == Vector2.zero ? 0 : Mathf.Atan2(_inputMoveVector.y, _inputMoveVector.x) * Mathf.Rad2Deg;
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
                float t = 1f - Mathf.Exp(-_directionSmoothness * Time.fixedDeltaTime);
                _directionAngle = Mathf.LerpAngle(_directionAngle, targetAngle, t);
            }

            _directionAngle = Mathf.Repeat(_directionAngle, 360f);

            await UniTask.Yield(Timing, token);
        }
    }

    private async UniTask MovementTask(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Vector2 offset = DirectionVector * _velocity;
            _rigidbody.MovePosition(_rigidbody.position + offset);

            PositionChanged?.Invoke();

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
    [Obsolete]
    public async UniTask VelocityCalculatingTask(bool accelerating, CancellationToken cancellationToken)
    {
        AnimationCurve curve = accelerating ? _accelerationCurve : _brakingCurve;
        float totalDuration = accelerating ? _accelerationDuration : BrakingDuration;

        float elapsed = 0f;
        while (!cancellationToken.IsCancellationRequested)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration);

            float curveValue = curve.Evaluate(t);
            _normalizedVelocity = accelerating ? curveValue : 1f - curveValue;
            _velocity = _normalizedVelocity * _maxVelocity;

            await UniTask.Yield(Timing, cancellationToken);
        }
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// Асинхронная корутина для плавного ускорения.
    /// Velocity растет от 0 до maxVelocity.
    /// normalizedVelocity всегда в диапазоне [0;1].
    /// </summary>
    public async UniTask AccelerationTask(CancellationToken cancellationToken)
    {
        // Ускоряемся с нуля
        while (_currentDuration < 1f && !cancellationToken.IsCancellationRequested)
        {
            _currentDuration += Time.deltaTime / _accelerationDuration;
            _currentDuration = Mathf.Clamp01(_currentDuration);

            float curveValue = _accelerationCurve.Evaluate(_currentDuration);
            _normalizedVelocity = curveValue;
            _velocity = _normalizedVelocity * _maxVelocity;

            await UniTask.Yield(Timing, cancellationToken);
        }
        await UniTask.CompletedTask;
    }

    /// <summary>
    /// Асинхронная корутина для плавного торможения.
    /// Velocity падает от maxVelocity до 0.
    /// normalizedVelocity всегда в диапазоне [0;1].
    /// </summary>
    public async UniTask BrakingTask(CancellationToken cancellationToken)
    {
        while (_currentDuration > 0f && !cancellationToken.IsCancellationRequested)
        {
            _currentDuration -= Time.deltaTime / BrakingDuration;
            _currentDuration = Mathf.Clamp01(_currentDuration);

            float curveValue = _brakingCurve.Evaluate(_currentDuration);
            _normalizedVelocity = curveValue;
            _velocity = _normalizedVelocity * _maxVelocity;

            await UniTask.Yield(Timing, cancellationToken);
        }
        _currentDuration = 0;
        await UniTask.CompletedTask;
    }

    public void StartAccelerationTask(bool restart = true)
    {
        if (_AccelerationCTS != null && !restart)
            return;

        if (_AccelerationCTS != null)
            StopAccelerationTask();

        _AccelerationCTS = new CancellationTokenSource();
        _accelerationTask = AccelerationTask(_AccelerationCTS.Token);
    }

    public void StopAccelerationTask()
    {
        if (_AccelerationCTS != null)
        {
            _AccelerationCTS.Cancel();
            _AccelerationCTS.Dispose();
            _AccelerationCTS = null;
        }
    }

    public void StartBrakingTask(bool restart = true)
    {
        if (_brakingCTS != null && !restart)
            return;

        if (_brakingCTS != null)
            StopBrakingTask();

        _brakingCTS = new CancellationTokenSource();
        _brakingTask = BrakingTask(_brakingCTS.Token);
    }

    public void StopBrakingTask()
    {
        if (_brakingCTS != null)
        {
            _brakingCTS.Cancel();
            _brakingCTS.Dispose();
            _brakingCTS = null;
        }
    }

    public void StartStateMonitoringTask(bool restart = true)
    {
        if (_stateMonitoringCTS != null && !restart)
            return;

        if (_stateMonitoringCTS != null)
        {
            StopStateMonitoringTask();
        }
        _stateMonitoringCTS = new CancellationTokenSource();
        _stateMonitoringTask = StateMonitoringTask(_stateMonitoringCTS.Token);
    }

    public void StopStateMonitoringTask()
    {
        if (_stateMonitoringCTS != null)
        {
            _stateMonitoringCTS.Cancel();
            //try
            //{
            //    await _stateMonitoringTask;
            //}
            //catch (OperationCanceledException)
            //{
            //    Debug.Log("State Monitoring Canceled");
            //}
            //catch (Exception ex)
            //{
            //    Debug.LogError(ex);
            //}
            _stateMonitoringCTS.Dispose();
            _stateMonitoringCTS = null;
        }
    }

    public void StartDirectionMonitoringTask
    (bool restart = true)
    {
        if (_directionCts != null && !restart)
            return; // Уже запущено и не требуется перезапуск

        if (_directionCts != null)
        {
            StopDirectionMonitoringTask();
        }
        _directionCts = new CancellationTokenSource();
        _directionTask = DirectionUpdateTask(_directionCts.Token);
    }

    public void StopDirectionMonitoringTask()
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

    [Obsolete]
    public void StartVelocityTask(bool accelerating, bool restart = true)
    {
        if (_velocityCTS != null && !restart)
            return; // Уже запущено и не требуется перезапуск

        if (_velocityCTS != null)
        {
            StopVelocityTask();
        }
        _velocityCTS = new CancellationTokenSource();
        _velocityTask = VelocityCalculatingTask(accelerating, _velocityCTS.Token);
    }

    [Obsolete]
    public void StopVelocityTask()
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

    public void StartMovementTask(bool restart = true)
    {
        if (_movementCTS != null && !restart)
            return; // Уже запущено и не требуется перезапуск

        if (_movementCTS != null)
        {
            StopMovementTask();
        }
        _movementCTS = new CancellationTokenSource();
        _movementTask = MovementTask(_movementCTS.Token);
    }

    public void StopMovementTask()
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

    public bool IsMoving()
    {
        return !_inputMoveVector.Equals(Vector2.zero);
    }

    public void Move(Vector2 direction)
    {
        _inputMoveVector = direction;
    }

    private void _stateMachine_OnEnterIdle()
    {
        StopDirectionMonitoringTask();
        StopBrakingTask();
        StopAccelerationTask();
        StopMovementTask();
    }

    private void _stateMachine_OnEnterAcceleration()
    {
        StopBrakingTask();

        StartDirectionMonitoringTask();
        StartAccelerationTask();

        StartMovementTask();

    }

    private void _stateMachine_OnEnterPerformed()
    {
        StopBrakingTask();
        StopAccelerationTask();

    }

    private void _stateMachine_OnEnterBraking()
    {

        StopAccelerationTask();
        StartBrakingTask();
    }


    private void _stateMachine_StateChanged(MovementState obj)
    {
        StateChanged?.Invoke(obj);
    }

    #region Unity Methods
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        StateChanged?.Invoke(MovementState.idle);
    }

    private void Update()
    {
    }

    private void FixedUpdate()
    {
        Debug.Log($"CurrentState : {CurrentState}, Velocity : {Velocity}, NormilizrdVelocity : {NormalizedVelocity}," +
            $" \n tasks \n StateMonitoring : {_stateMonitoringTask.Status}, DirectionMonitoring : {_directionTask.Status}" +
            $"MovementTask : {_movementTask.Status}, AccelerationTask : {_accelerationTask.Status}, BrakingTask : {_brakingTask.Status}");
    }

    private void OnEnable()
    {
        _stateMachine.OnEnterIdle += _stateMachine_OnEnterIdle;
        _stateMachine.OnEnterAcceleration += _stateMachine_OnEnterAcceleration;
        _stateMachine.OnEnterPerformed += _stateMachine_OnEnterPerformed;
        _stateMachine.OnEnterBraking += _stateMachine_OnEnterBraking;

        _stateMachine.StateChanged += _stateMachine_StateChanged;

        StartStateMonitoringTask();
    }


    private void OnDisable()
    {

        _stateMachine.OnEnterIdle -= _stateMachine_OnEnterIdle;
        _stateMachine.OnEnterAcceleration -= _stateMachine_OnEnterAcceleration;
        _stateMachine.OnEnterPerformed -= _stateMachine_OnEnterPerformed;
        _stateMachine.OnEnterBraking -= _stateMachine_OnEnterBraking;

        _stateMachine.StateChanged -= _stateMachine_StateChanged;

        StopStateMonitoringTask();
        StopDirectionMonitoringTask();
        StopMovementTask();
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
