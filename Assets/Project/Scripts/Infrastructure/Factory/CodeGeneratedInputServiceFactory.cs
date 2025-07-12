using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Warm.Project.Infrastructure.Factory
{
    public class CodeGeneratedInputServiceFactory : IFactory<InputService>
    {
        public const string INPUT_KEY_MOVEMENT = "movement";

        private DiContainer _container;

        public CodeGeneratedInputServiceFactory(DiContainer container)
        {
            _container = container;
        }

        public InputService Create()
        {
            EventBus.EventBus eventBus = _container.Resolve<EventBus.EventBus>();
            InputSystem_Actions actions = _container.Resolve<InputSystem_Actions>();

            InputSubscribersContainer subscribers = CreateSubscribers(actions, eventBus);

            InputService service = new(eventBus, actions, subscribers);
            return service;
        }

        private InputSubscribersContainer CreateSubscribers(InputSystem_Actions actions, EventBus.EventBus eventBus)
        {
            InputSubscribersContainer container = new InputSubscribersContainer();

            container.SubscribePerformed(INPUT_KEY_MOVEMENT, actions.Player.Move, OnInputMovement);
            container.SubscribeCanceled(INPUT_KEY_MOVEMENT, actions.Player.Move, OnInputMovement);

            void OnInputMovement(InputAction.CallbackContext context)
            {
                var value = context.ReadValue<Vector2>();
                eventBus.RaiseEvent<IPlayerMovementHandler>(h => h.HandleMovement(value));
            }

            return container;
        }


        private void OnMovementHandler(InputAction.CallbackContext context)
        {
            throw new NotImplementedException();
        }
    }
}