[InteractableComponent]
public class DestroyInteractionHandler : InteractableHandlerBehaviourBase
{
    public override void HandleInteract(InteractionContext context)
    {
        Destroy(gameObject);
    }
}