using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class InteractionBehaviour : MonoBehaviour
{
    public float Radius;
    public float Distance;

    public InteractableBehaviour SelectedInteractable => Interactables.First();

    public InteractableBehaviour[] Interactables;

    public LayerMask Mask;

    public PlayerLoopTiming Timing => _timing;

    private UniTaskCoroutine _interactionMonitoringUniTaskCorutine;

    [SerializeField]
    private PlayerLoopTiming _timing;

    private async UniTask InteractableMonitoring(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Interactables = GetInteractables().ToArray();

            await UniTask.Yield(_timing, cancellationToken);
        }
    }

    public IEnumerable<InteractableBehaviour> FindeInteractables(Vector2 origin, float radius, float distance, LayerMask mask)
    {
        // —оберЄм все попадани€
        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin,      // точка старта
            radius,      // радиус круга
            Vector2.zero, // направление (Vector2.zero Ч ищем только на месте, без перемещени€)
            distance,    // дистанци€ круга, если нужно "т€нуть" круг вперЄд (или просто 0)
            mask         // слой
        );

        // —оберЄм все уникальные InteractableBehaviour
        HashSet<InteractableBehaviour> result = new HashSet<InteractableBehaviour>();
        foreach (var hit in hits)
        {
            var interactable = hit.collider.GetComponent<InteractableBehaviour>();
            if (interactable != null)
            {
                result.Add(interactable);
            }
        }
        return result;
    }

    public IEnumerable<InteractableBehaviour> GetInteractables()
    {
        return FindeInteractables(transform.position, Radius, Distance, Mask).OrderBy((item) => Vector2.Distance(transform.position, item.transform.position));
    }

    public void Interact()
    {
        SelectedInteractable.Interact(this);
    }

    #region Unity Methods

    private void Awake()
    {
        _interactionMonitoringUniTaskCorutine = new(InteractableMonitoring);
    }

    private void OnEnable()
    {
        _interactionMonitoringUniTaskCorutine.Run();
    }

    private void OnDisable()
    {
        _interactionMonitoringUniTaskCorutine.Stop();
    }

    private void OnDestroy()
    {
        _interactionMonitoringUniTaskCorutine.Dispose();
    }
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
    #endregion
}
