using NoSlimes.Logging;
using System;
using System.Collections.Generic;

namespace RacingGame
{
    public class StateMachine
    {
        public abstract class State
        {
            public virtual void Enter() { }
            public virtual void Exit() { }
            public virtual void Update() { }
            public virtual void LateUpdate() { }
            public virtual void FixedUpdate() { }
        }

        private readonly Dictionary<Type, State> states = new();
        private State currentState;

        private readonly DLogCategory logCategory;

        public StateMachine(IList<State> states, DLogCategory logCategory = null)
        {
            foreach (State state in states)
            {
                AddState(state);
            }

            this.logCategory = logCategory ?? DLogCategory.Log;

            if (states.Count > 0)
            {
                currentState = states[0];
                currentState.Enter();
            }
        }

        public void AddState(State state)
        {
            Type type = state.GetType();

            if (states.TryGetValue(type, out _))
            {
                DLogger.LogDevError($"State of type {type} already exists in the StateMachine.", category: logCategory);
                return;
            }

            states[type] = state;
        }

        public void RemoveState<T>() where T : State
        {
            Type type = typeof(T);

            if (!states.Remove(type))
            {
                DLogger.LogDevError($"State of type {type} does not exist in the StateMachine.", category: logCategory);
            }
        }

        public void ChangeState<T>() where T : State
        {
            Type type = typeof(T);

            if (!states.TryGetValue(type, out var nextState))
            {
                DLogger.LogDevError($"State of type {type} does not exist in the StateMachine.", category: logCategory);
                return;
            }

            currentState?.Exit();
            currentState = nextState;
            currentState.Enter();
        }

        public T GetState<T>() where T : State
        {
            Type type = typeof(T);

            if (states.TryGetValue(type, out var state))
            {
                return (T)state;
            }

            DLogger.LogDevError($"State of type {type} does not exist in the StateMachine.", category: logCategory);

            return null;
        }

        public void Update()
        {
            currentState?.Update();
        }

        public void LateUpdate()
        {
            currentState?.LateUpdate();
        }

        public void FixedUpdate()
        {
            currentState?.FixedUpdate();
        }
    }
}
