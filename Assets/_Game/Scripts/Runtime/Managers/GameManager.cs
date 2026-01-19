using NoSlimes.Logging;
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
                new GameState(this, logCategory),
            }, stateMachineLogCategory);

            DLogger.LogDev("GameManager initialized.", category: logCategory);
        }

        private void Update()
        {
            StateMachine.Update();
        }

        private void LateUpdate()
        {
            StateMachine.LateUpdate();
        }

        private void FixedUpdate()
        {
            StateMachine.FixedUpdate();
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
        public GameState(GameManager gameManager, DLogCategory logCategory) : base(gameManager, logCategory) { }
        private readonly List<ITickable> tickables = new();

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
            var tickables = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ITickable>();
            foreach (var tickable in tickables)
            {
                this.tickables.Add(tickable);
            }

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

    public interface ITickable
    {
        public virtual int Priority => 0;

        public void Tick();
        public void LateTick();
        public void FixedTick();
    }
}
