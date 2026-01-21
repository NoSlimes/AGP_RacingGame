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
    [DefaultExecutionOrder(-100)]
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
        private CarSpawner carSpawner;

        public StateMachine StateMachine { get; private set; }
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
                new GameState(this, stateMachineLogCategory),
                new PauseState(this, stateMachineLogCategory)
            }, stateMachineLogCategory);

            IEnumerable<ITickable> initialTickables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ITickable>();
            foreach (ITickable t in initialTickables) RegisterTickable(t);

            DLogger.LogDev("GameManager initialized.", category: logCategory);
        }

        private void Start()
        {
            var waypointBuilder = FindFirstObjectByType<TrackWaypointBuilder>();

            carSpawner = new(waypointBuilder, carPrefab, carCount, spawnPlayerCar);
            carSpawner.OnCarsSpawned += () => OnPlayerCarAssigned?.Invoke(GetPlayerCar());

            if (autoSpawn)
                StartCoroutine(SpawnCoroutine());
        }

        private IEnumerator SpawnCoroutine()
        {
            yield return null; // Yield one frame to ensure everything is initialized
            carSpawner.SpawnCars();
        }

        [ConsoleCommand("spawn_cars", "Spawns cars into the scene.")]
        private void SpawnCars(CommandResponseDelegate response)
        {
            Car[] spawnedCars = carSpawner.SpawnCars();
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

        public Car GetPlayerCar() => carSpawner?.PlayerCar;
        public Car[] GetAllCars() => carSpawner?.SpawnedCars.ToArray() ?? Array.Empty<Car>();

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

    public static class CarInputExtensions
    {
        public static bool IsPlayerCar(this CarInputComponent comp)
        {
            return comp.Inputs is PlayerCarInputs;
        }
    }
}