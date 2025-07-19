using UnityEngine;

namespace Warm.Project.Scripts.behaviours.Interaction.InteractableHandlers
{
    [InteractableComponent]
    public class DebugInteracting : InteractableHandlerBehaviourBase
    {
        public override void HandleInteract(InteractionContext context)
        {
            Debug.Log(this.ToString() + " has interacted");
        }
    }
}
