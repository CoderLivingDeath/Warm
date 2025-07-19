using Cysharp.Threading.Tasks;
using System;
using System.Threading;
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

    // corutines
    private UniTaskCoroutine _movementUniTaskCorutine;

    private UniTaskCoroutine _directionUniTaskCorutine;

    private UniTaskCoroutine _stateMonitoringUniTaskCorutine;

    private UniTaskCoroutine _accelerationUniTaskCoritine;

    private UniTaskCoroutine _brakingUniTaskCorutine;

    private DisposerContainer _disposerContainer;

    #region Corutines
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
            Vector2 prevInputMoveVector = _inputMoveVector;

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

            await UniTask.Yield(PlayerLoopTiming.LastFixedUpdate, cancellationToken); // TODO: исправить несовподающие тайминги. LastFixedUpdate выбран для правильного порядка вызова
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
    #endregion

    private void _stateMachine_OnEnterIdle()
    {
        _brakingUniTaskCorutine.Stop();
        _accelerationUniTaskCoritine.Stop();
        _movementUniTaskCorutine.Stop();
    }

    private void _stateMachine_OnEnterAcceleration()
    {
        _brakingUniTaskCorutine.Stop();

        _directionUniTaskCorutine.Run();
        _accelerationUniTaskCoritine.Run();

        _movementUniTaskCorutine.Run();
    }

    private void _stateMachine_OnEnterPerformed()
    {
        _brakingUniTaskCorutine.Stop();
        _accelerationUniTaskCoritine.Stop();
    }

    private void _stateMachine_OnEnterBraking()
    {

        _accelerationUniTaskCoritine.Stop();
        _brakingUniTaskCorutine.Run();
    }

    public bool IsMoving()
    {
        return !_inputMoveVector.Equals(Vector2.zero);
    }

    public void Move(Vector2 direction)
    {
        _inputMoveVector = direction;
    }

    #region Handlers

    private void _stateMachine_StateChanged(MovementState obj)
    {
        StateChanged?.Invoke(obj);
    }

    #endregion

    #region Unity Methods
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();

        _movementUniTaskCorutine = new(MovementTask);
        _directionUniTaskCorutine = new(DirectionUpdateTask);
        _stateMonitoringUniTaskCorutine = new(StateMonitoringTask);
        _accelerationUniTaskCoritine = new(AccelerationTask);
        _brakingUniTaskCorutine = new(BrakingTask);

        _disposerContainer = new();

        _disposerContainer.Add(_movementUniTaskCorutine);
        _disposerContainer.Add(_directionUniTaskCorutine);
        _disposerContainer.Add(_stateMonitoringUniTaskCorutine);
        _disposerContainer.Add(_accelerationUniTaskCoritine);
        _disposerContainer.Add(_brakingUniTaskCorutine);
    }

    private void Start()
    {
        StateChanged?.Invoke(MovementState.idle);
    }

    private void OnEnable()
    {
        _stateMachine.OnEnterIdle += _stateMachine_OnEnterIdle;
        _stateMachine.OnEnterAcceleration += _stateMachine_OnEnterAcceleration;
        _stateMachine.OnEnterPerformed += _stateMachine_OnEnterPerformed;
        _stateMachine.OnEnterBraking += _stateMachine_OnEnterBraking;

        _stateMachine.StateChanged += _stateMachine_StateChanged;

        _stateMonitoringUniTaskCorutine.Run();
        _directionUniTaskCorutine.Run();
    }

    private void OnDisable()
    {

        _stateMachine.OnEnterIdle -= _stateMachine_OnEnterIdle;
        _stateMachine.OnEnterAcceleration -= _stateMachine_OnEnterAcceleration;
        _stateMachine.OnEnterPerformed -= _stateMachine_OnEnterPerformed;
        _stateMachine.OnEnterBraking -= _stateMachine_OnEnterBraking;

        _stateMachine.StateChanged -= _stateMachine_StateChanged;

        _stateMonitoringUniTaskCorutine.Stop();
        _directionUniTaskCorutine.Stop();
        _movementUniTaskCorutine.Stop();
    }

    private void OnDestroy()
    {
        _disposerContainer.Dispose();
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
