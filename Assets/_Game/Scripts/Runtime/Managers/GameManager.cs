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
        public StateMachine StateMachine { get; private set; }

        public bool IsPaused => Instance.StateMachine.CurrentState is not null and PauseState;
        public event StateMachine.StateChangedDelegate StateChanged
        {
            add => StateChanged += value;
            remove => StateChanged -= value;
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

            DLogger.LogDev("GameManager initialized.", category: logCategory);
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
            if (!InputManager.Instance)
                return;

            InputManager.Instance.UnregisterActionCallback("Cancel", OnPausePressed, InputManager.InputEventType.Performed);
        }

        private void OnPausePressed(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsPaused)
            {
                StateMachine.ChangeState<GameState>();
            }
            else
            {
                StateMachine.ChangeState<PauseState>();
            }
        }
    }

    public abstract class GameManagerState : StateMachine.State
    {
        protected GameManager GameManager { get; private set; }
        protected DLogCategory LogCategory { get; private set; }

        public GameManagerState(GameManager gameManager, DLogCategory logCategory)
        {
            GameManager = gameManager;
            LogCategory = logCategory ?? DLogCategory.Log;
        }
    }

    public class GameState : GameManagerState
    {
        private readonly List<ITickable> tickables = new();
        
        public GameState(GameManager gameManager, DLogCategory logCategory) : base(gameManager, logCategory)
        {
            if (!Application.isPlaying)
                return;

            var tickables = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ITickable>();
            foreach (var tickable in tickables)
            {
                this.tickables.Add(tickable);
            }
        }

        public void RegisterTickable(ITickable tickable)
        {
            if (!tickables.Contains(tickable))
            {
                tickables.Add(tickable);
            }

            tickables.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void UnregisterTickable(ITickable tickable)
        {
            if (tickables.Contains(tickable))
            {
                tickables.Remove(tickable);
            }

            tickables.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public override void Enter()
        {
            base.Enter();

            DLogger.LogDev("Entered GameState.", category: LogCategory);
        }

        public override void Update()
        {
            base.Update();
            foreach (var tickable in tickables)
            {
                tickable.Tick();
            }
        }

        public override void LateUpdate()
        {
            base.LateUpdate();
            foreach (var tickable in tickables)
            {
                tickable.LateTick();
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            foreach (var tickable in tickables)
            {
                tickable.FixedTick();
            }
        }
    }

    public class PauseState : GameManagerState
    {
        public PauseState(GameManager gameManager, DLogCategory logCategory) : base(gameManager, logCategory) { }

        private float prevTimeScale = 0f;

        public event Action OnPause;
        public event Action OnResume;

        public override void Enter()
        {
            base.Enter();

            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            OnPause?.Invoke();
            DLogger.LogDev("Game Paused.", category: LogCategory);
        }
        public override void Exit()
        {
            base.Exit();

            Time.timeScale = prevTimeScale;

            OnResume?.Invoke();
            DLogger.LogDev("Game Resumed.", category: LogCategory);
        }
    }
}
