using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Warm.Project.Infrastructure.EventBus;
using Zenject;

namespace Warm.Project.Infrastructure.Factory
{
    public class CodeGeneratedInputServiceFactory : IFactory<InputService>
    {
        public const string INPUT_KEY_MOVEMENT = "movement";
        public const string INPUT_KEY_INTERACT = "interact";

        private DiContainer _container;

        public CodeGeneratedInputServiceFactory(DiContainer container)
        {
            _container = container;
        }

        public InputService Create()
        {
            EventBus.EventBus eventBus = _container.Resolve<EventBus.EventBus>();
            InputSystem_Actions actions = _container.Resolve<InputSystem_Actions>();

            InputSubscribersContainer subscribersContainer = CreateSubscribers(actions, eventBus);

            InputService service = new(eventBus, actions, subscribersContainer);
            return service;
        }

        private InputSubscribersContainer CreateSubscribers(InputSystem_Actions actions, EventBus.EventBus eventBus)
        {
            InputSubscribersContainer container = new InputSubscribersContainer();

            container.SubscribePerformed(INPUT_KEY_MOVEMENT, actions.Player.Move, OnInputMovement);
            container.SubscribeCanceled(INPUT_KEY_MOVEMENT, actions.Player.Move, OnInputMovement);

            container.SubscribePerformed(INPUT_KEY_INTERACT, actions.Player.Interact, OnInteract);

            void OnInputMovement(InputAction.CallbackContext context)
            {
                var value = context.ReadValue<Vector2>();
                eventBus.RaiseEvent<IPlayerMovementHandler>(h => h.HandleMovement(value));
            }

            void OnInteract(InputAction.CallbackContext context)
            {
                eventBus.RaiseEvent<IPlayerInteractionHandler>(h => h.HandleInteraction());
            }

            return container;
        }
    }
}