using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMover : MonoBehaviour
{
    public float StoppingDistance = 1f;

    public MovementBehaviour Character;

    private CancellationTokenSource _clickMonitoringCTS = new CancellationTokenSource();

    public async UniTask MonitoringMouseClick(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await UniTask.WaitUntil(() => Mouse.current.leftButton.isPressed, PlayerLoopTiming.Update);
            if (Character != null)
            {
                Vector2 pointOnScreen = Mouse.current.position.value;
                Vector2 toWorldPoint = Camera.main.ScreenToWorldPoint(pointOnScreen);

                float distance = Vector2.Distance(Character.Position, toWorldPoint);
                int iterationCount = (int)(distance / Character.MaxVelocity) * 2;
                try
                {
                    await MoveTo(Character, toWorldPoint, iterationCount, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("Move To Canceled");
                    Character.Move(Vector2.zero);
                }
            }
            else
            {
                Debug.Log("Character is null");
            }

            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
        }
    }

    public async UniTask MoveTo(MovementBehaviour character, Vector2 To, int iterationCount, CancellationToken cancellationToken)
    {
        int currentIteration = 0;

        currentIteration++;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (currentIteration >= iterationCount)
            {
                break;
            }

            currentIteration++;

            float distance = Vector2.Distance(character.Position, To);
            if (distance < StoppingDistance)
            {
                break;
            }

            Vector2 direction = To - (Vector2)character.transform.position;

            character.Move(direction.normalized);

            await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
        }
        character.Move(Vector2.zero);
    }

    private void Start()
    {
        try
        {
            MonitoringMouseClick(_clickMonitoringCTS.Token).Forget();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Mouse monitoring canceled");
        }
    }

    private void OnDisable()
    {
        _clickMonitoringCTS?.Cancel();
        _clickMonitoringCTS?.Dispose();
    }
}
