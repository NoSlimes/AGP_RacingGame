using NoSlimes.Logging;
using NoSlimes.UnityUtils.Input;
using NoSlimes.Util.UniTerminal;
using RacingGame._Game.Scripts.PCG;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RacingGame
{
    [DefaultExecutionOrder(-9)]
    public partial class GameManager : MonoBehaviour
    {
        private static readonly DLogCategory logCategory = new("GameManager", Color.green);
        private static readonly DLogCategory stateMachineLogCategory = new("StateMachine", Color.yellowGreen);
        public static GameManager Instance { get; private set; }

        [SerializeField] private Car carPrefab;
        [SerializeField] private int carCount = 2;
        [SerializeField] private bool autoSpawn = true;
        [SerializeField] private bool spawnPlayerCar = true;

        private readonly List<ITickable> tickables = new();

        public CarSpawner CarSpawner {  get; private set; }
        public StateMachine StateMachine { get; private set; }
        public CheckpointManager CheckpointManager { get; private set; }
        public bool IsPaused => StateMachine?.CurrentState is PauseState;

        public event Action<Car> OnPlayerCarAssigned;
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
                new GameState(this, stateMachineLogCategory, autoSpawn),
                new PauseState(this, stateMachineLogCategory)
            }, stateMachineLogCategory);
            
            CheckpointManager = FindFirstObjectByType<CheckpointManager>();

            IEnumerable<ITickable> initialTickables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ITickable>();
            foreach (ITickable t in initialTickables) RegisterTickable(t);

            DLogger.LogDev("GameManager initialized.", category: logCategory);
        }

        private void Start()
        {
            var waypointBuilder = FindFirstObjectByType<TrackWaypointBuilder>();

            CarSpawner = new(waypointBuilder, carPrefab, carCount, spawnPlayerCar);
            CarSpawner.OnCarsSpawned += () =>
            {
                var playerCar = GetPlayerCar();
                if (playerCar != null)
                {
                    DLogger.LogDev("Player car assigned.", category: logCategory);

                    OnPlayerCarAssigned?.Invoke(playerCar);
                }
            };
        }

        [ConsoleCommand("spawn_cars", "Spawns cars into the scene.")]
        private void SpawnCars(CommandResponseDelegate response)
        {
            Car[] spawnedCars = CarSpawner.SpawnCars();
            response($"Spawned {spawnedCars.Length} cars into the scene.");
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

        public Car GetPlayerCar() => CarSpawner?.PlayerCar;

        [Obsolete("Use GameManager.AllCars instead")]
        public Car[] GetAllCars() => CarSpawner?.SpawnedCars.ToArray() ?? Array.Empty<Car>();

        public IReadOnlyList<Car> AllCars => CarSpawner?.SpawnedCars;

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

#if DEBUG
        private void OnDrawGizmos()
        {
            foreach (var tickable in tickables)
            {
                tickable.DrawDebug();
            }

            foreach (var spawnPoint in CarSpawner?.SpawnPositions ?? Array.Empty<(Vector3, Vector3)>())
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(spawnPoint.spawnPoint, 0.5f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(spawnPoint.spawnPoint, spawnPoint.spawnPoint + spawnPoint.trackForward * 2.0f);
            }
        }
#endif

        private void Update() => StateMachine.Update();
        private void LateUpdate() => StateMachine.LateUpdate();
        private void FixedUpdate() => StateMachine.FixedUpdate();

        private void OnEnable()
        {
            InputManager.Instance.RegisterActionCallback("Pause", OnPausePressed, InputManager.InputEventType.Performed);
        }

        private void OnDisable()
        {
            if (InputManager.Instance)
                InputManager.Instance.UnregisterActionCallback("Pause", OnPausePressed, InputManager.InputEventType.Performed);
        }

        private void OnPausePressed(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsPaused) StateMachine.ChangeState<GameState>();
            else StateMachine.ChangeState<PauseState>();
        }
    }

    public static class CarInputExtensions
    {
        public static bool IsPlayerCar(this CarInputComponent comp)
        {
            return comp.Inputs is PlayerCarInputs;
        }
    }
}