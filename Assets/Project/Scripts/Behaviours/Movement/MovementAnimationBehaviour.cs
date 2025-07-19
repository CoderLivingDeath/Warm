using System;
using System.Linq;
using UnityEngine;
using Warm.Project.Scripts.Movement;

public class MovementAnimationBehaviour : MonoBehaviour
{
    public const string PLAYER_ANIMATION_TRIGGER_IDLE = "IsIdle";
    public const string PLAYER_ANIMATION_TRIGGER_MOVE = "IsMove";

    public bool FlipToDirection => _flipToDirection;
    [SerializeField]
    private bool _flipToDirection = true;

    private string[] _stateTriggers = new string[]
    {
    PLAYER_ANIMATION_TRIGGER_IDLE,
    PLAYER_ANIMATION_TRIGGER_MOVE
    };

    public Animator Animator => _animator;
    [SerializeField]
    private Animator _animator;

    public SpriteRenderer SpriteRenderer => _spriteRenderer;
    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    public MovementBehaviour MovementBehaviour => _movementBehaviour;
    [SerializeField]
    private MovementBehaviour _movementBehaviour;
    public void SetStateTrigger(string triggerName)
    {
        // Проверяем, что триггер находится в списке
        if (!_stateTriggers.Contains(triggerName))
        {
            Debug.LogError($"Триггер '{triggerName}' не принадлежит к списку stateTriggers и не будет сброшен должным образом.");
            return;
        }

        ResetAllStateTriggers();
        _animator.SetTrigger(triggerName);
    }

    private void ResetAllStateTriggers()
    {
        foreach (var trigger in _stateTriggers)
        {
            _animator.ResetTrigger(trigger);
        }
    }
    private void SetIdle()
    {
        SetStateTrigger(PLAYER_ANIMATION_TRIGGER_IDLE);
    }

    private void SetAcceleration()
    {
        SetStateTrigger(PLAYER_ANIMATION_TRIGGER_MOVE);
    }

    private void SetPerformed()
    {
        SetStateTrigger(PLAYER_ANIMATION_TRIGGER_MOVE);
    }
    private void SetBraking()
    {
        SetStateTrigger(PLAYER_ANIMATION_TRIGGER_MOVE);
    }

    public void SwitchAnimationState(MovementState state)
    {

        switch (state)
        {
            case MovementState.idle:
                SetIdle();
                break;
            case MovementState.acceleration:
                SetAcceleration();
                break;
            case MovementState.performed:
                SetPerformed();
                break;
            case MovementState.braking:
                SetBraking();
                break;
        }
    }

    private void ValidateAnimator(Animator animator)
    {
        // Проверяем существование параметра IsIdle
        if (!ParameterExists(animator, PLAYER_ANIMATION_TRIGGER_IDLE, AnimatorControllerParameterType.Trigger))
        {
            Debug.LogError($"Параметр аниматора '{PLAYER_ANIMATION_TRIGGER_IDLE}' не найден или имеет неверный тип!");
        }

        // Проверяем существование параметра IsMove
        if (!ParameterExists(animator, PLAYER_ANIMATION_TRIGGER_MOVE, AnimatorControllerParameterType.Trigger))
        {
            Debug.LogError($"Параметр аниматора '{PLAYER_ANIMATION_TRIGGER_MOVE}' не найден или имеет неверный тип!");
        }
    }

    private bool ParameterExists(Animator animator, string paramName, AnimatorControllerParameterType paramType)
    {
        if (animator == null || !animator.isInitialized || animator.parameters == null)
        {
            return false;
        }

        foreach (var param in animator.parameters)
        {
            if (param.name == paramName && param.type == paramType)
            {
                return true;
            }
        }
        return false;
    }


    private void _movementBehaviour_PositionChanged()
    {
        float angle = _movementBehaviour.DirectionAngle;
        bool shouldFlip = angle > 90f && angle < 270f;

        if (_spriteRenderer.flipX != shouldFlip)
        {
            _spriteRenderer.flipX = shouldFlip;
        }
    }

    private void _movementBehaviour_StateChanged(MovementState obj)
    {
        SwitchAnimationState(obj);
    }


    #region Unity Methods

    private void Start()
    {
        ValidateAnimator(_animator);
    }

    private void OnEnable()
    {
        if (FlipToDirection)
        {
            _movementBehaviour.PositionChanged += _movementBehaviour_PositionChanged;
        }
        _movementBehaviour.StateChanged += _movementBehaviour_StateChanged;
    }


    private void OnDisable()
    {
        if (FlipToDirection)
        {
            _movementBehaviour.PositionChanged -= _movementBehaviour_PositionChanged;
        }
        _movementBehaviour.StateChanged -= _movementBehaviour_StateChanged;
    }


    #endregion
}
