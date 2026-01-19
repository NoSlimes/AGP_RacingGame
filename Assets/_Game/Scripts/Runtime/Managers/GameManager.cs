using NoSlimes.Logging;
using NoSlimes.UnityUtils.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RacingGame
{
    public class GameManager : MonoBehaviour
    {
        private static readonly DLogCategory logCategory = new("GameManager", Color.green);
        private static readonly DLogCategory stateMachineLogCategory = new("StateMachine", Color.yellowGreen);
        public static GameManager Instance { get; private set; }

        private readonly List<ITickable> tickables = new();

        public StateMachine StateMachine { get; private set; }
        public bool IsPaused => StateMachine?.CurrentState is PauseState;

        public event StateMachine.StateChangedDelegate StateChanged
        {
            add => StateMachine.OnStateChanged += value;
            remove => StateMachine.OnStateChanged -= value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            StateMachine = new StateMachine(new List<StateMachine.State>
            {
                new GameState(this, stateMachineLogCategory),
                new PauseState(this, stateMachineLogCategory)
            }, stateMachineLogCategory);

            var initialTickables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ITickable>();
            foreach (var t in initialTickables) RegisterTickable(t);

            DLogger.LogDev("GameManager initialized.", category: logCategory);
        }

        public void RegisterTickable(ITickable tickable)
        {
            if (tickable == null || tickables.Contains(tickable)) return;

            tickables.Add(tickable);
            tickables.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void UnregisterTickable(ITickable tickable)
        {
            if (tickables.Remove(tickable))
            {

            }
        }

        public void UpdateTickables()
        {
            for (int i = 0; i < tickables.Count; i++) tickables[i].Tick();
        }

        public void LateUpdateTickables()
        {
            for (int i = 0; i < tickables.Count; i++) tickables[i].LateTick();
        }

        public void FixedUpdateTickables()
        {
            for (int i = 0; i < tickables.Count; i++) tickables[i].FixedTick();
        }

        private void Update() => StateMachine.Update();
        private void LateUpdate() => StateMachine.LateUpdate();
        private void FixedUpdate() => StateMachine.FixedUpdate();

        private void OnEnable()
        {
            InputManager.Instance.RegisterActionCallback("Cancel", OnPausePressed, InputManager.InputEventType.Performed);
        }

        private void OnDisable()
        {
            if (InputManager.Instance)
                InputManager.Instance.UnregisterActionCallback("Cancel", OnPausePressed, InputManager.InputEventType.Performed);
        }

        private void OnPausePressed(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsPaused) StateMachine.ChangeState<GameState>();
            else StateMachine.ChangeState<PauseState>();
        }
    }

    public abstract class GameManagerState : StateMachine.State
    {
        protected GameManager GameManager { get; private set; }
        protected DLogCategory LogCategory { get; private set; }

        public GameManagerState(GameManager gameManager, DLogCategory logCategory)
        {
            GameManager = gameManager;
            LogCategory = logCategory;
        }
    }

    public class GameState : GameManagerState
    {
        public GameState(GameManager gameManager, DLogCategory logCategory) : base(gameManager, logCategory) { }

        public override void Enter()
        {
            DLogger.LogDev("Entered GameState - Race Running.", category: LogCategory);
        }

        public override void Update()
        {
            GameManager.UpdateTickables();
        }

        public override void LateUpdate()
        {
            GameManager.LateUpdateTickables();
        }

        public override void FixedUpdate()
        {
            GameManager.FixedUpdateTickables();
        }
    }

    public class PauseState : GameManagerState
    {
        private float prevTimeScale = 1f;
        public PauseState(GameManager gameManager, DLogCategory logCategory) : base(gameManager, logCategory) { }

        public override void Enter()
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            DLogger.LogDev("Game Paused.", category: LogCategory);
        }

        public override void Exit()
        {
            Time.timeScale = prevTimeScale;
            DLogger.LogDev("Game Resumed.", category: LogCategory);
        }
    }
}