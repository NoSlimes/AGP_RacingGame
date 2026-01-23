using NoSlimes.Logging;
using RacingGame._Game.Scripts.PCG;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Splines;

namespace RacingGame
{
    public partial class GameState : GameManagerState
    {
        public GameState(GameManager gameManager, DLogCategory logCategory, bool autoSpawnCars) : base(gameManager, logCategory) => this.autoSpawnCars = autoSpawnCars;

        private bool hasEnteredBefore = false;
        private readonly bool autoSpawnCars = true;

        private TrackWaypointBuilder waypointBuilder;
        private BezierTrackGenerator bezierTrackGenerator;

        public override void Enter()
        {
            base.Enter();

            GameManager.CheckpointManager.OnCarPassedCheckpoint += OnCarPassedCheckpoint;
            DLogger.LogDev("Entered GameState", category: LogCategory);

            if (!hasEnteredBefore)
            {
                hasEnteredBefore = true;

                if (autoSpawnCars)
                    SpawnCars();
            }

            waypointBuilder = UnityEngine.Object.FindAnyObjectByType<TrackWaypointBuilder>();
            bezierTrackGenerator = UnityEngine.Object.FindAnyObjectByType<BezierTrackGenerator>();

            SetupCarRecovery();
        }

        public override void Exit()
        {
            base.Exit();

            GameManager.CheckpointManager.OnCarPassedCheckpoint -= OnCarPassedCheckpoint;
        }

        public override void Update()
        {
            GameManager.UpdateTickables();

            TickCarPlacements();
            TickRaceFinishTimer();
        }

        public override void LateUpdate()
        {
            GameManager.LateUpdateTickables();
        }

        public override void FixedUpdate()
        {
            GameManager.FixedUpdateTickables();

            TickCarRecovery();
        }
    }

    public partial class GameState // GameState.Spawning.cs
    {
        private void SpawnCars()
        {
            GameManager.StartCoroutine(SpawnCoroutine());
        }

        private IEnumerator SpawnCoroutine()
        {
            yield return null; // Yield one frame to ensure everything is initialized
            GameManager.CarSpawner.SpawnCars();
            SetupLaps();
        }
    }

    public partial class GameState // GameState.LapTracking.cs
    {
        private readonly Dictionary<Car, int> carLaps = new();
        private readonly Dictionary<Car, int> checkPointsHitThisLap = new();

        private readonly Dictionary<Car, float> carFinishTimes = new();
        private readonly List<Car> finishOrderSnapshot = new();

        private bool raceFinishTimerRunning;
        private float raceFinishStartTime;
        private const float raceFinishTimeout = 30f;
        private bool raceFinished;

        private readonly List<Car> carPlacements = new();
        public IReadOnlyList<Car> CarPlacements => carPlacements;

        public event Action<IReadOnlyList<Car>> OnCarPlacementsChanged;
        public event Action<Car, int> OnCarLapped;
        public event Action<Car, int> OnCarFinishedRace;
        public event Action<IReadOnlyList<(Car car, float time)>> OnRaceFinished;

        private int lastCheckFrame;

        private void SetupLaps()
        {
            carLaps.Clear();
            checkPointsHitThisLap.Clear();
            carFinishTimes.Clear();
            finishOrderSnapshot.Clear();
            raceFinishTimerRunning = false;
            raceFinished = false;

            foreach (var car in GameManager.AllCars)
                carLaps[car] = 0;
        }

        private void TickCarPlacements()
        {
            if (Time.frameCount - lastCheckFrame < 10)
                return;
            lastCheckFrame = Time.frameCount;

            var splineContainer = bezierTrackGenerator.splineContainer;
            if (splineContainer == null) return;

            var spline = splineContainer.Spline;
            float splineLength = spline.GetLength();

            foreach (var car in GameManager.AllCars)
            {
                float3 localPos = splineContainer.transform.InverseTransformPoint(car.transform.position);
                SplineUtility.GetNearestPoint(spline, localPos, out _, out float t);

                float distanceAlongSpline = spline.ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Distance);
                int laps = carLaps.TryGetValue(car, out int l) ? l : 0;

                car.ProgressScore = (laps * splineLength) + distanceAlongSpline;

                if (!carPlacements.Contains(car))
                    carPlacements.Add(car);
            }

            var oldOrder = carPlacements.ToList();
            carPlacements.Sort((a, b) => b.ProgressScore.CompareTo(a.ProgressScore));

            if (!carPlacements.SequenceEqual(oldOrder))
                OnCarPlacementsChanged?.Invoke(carPlacements);
        }

        private void OnCarPassedCheckpoint(Car car, int newCheckpointIdx, int lastCheckpointIdx, int totalCheckpointCount)
        {
            if (!carLaps.ContainsKey(car)) carLaps[car] = 0;
            if (!checkPointsHitThisLap.ContainsKey(car)) checkPointsHitThisLap[car] = 0;

            checkPointsHitThisLap[car]++;

            if (lastCheckpointIdx >= totalCheckpointCount - 3 &&
                checkPointsHitThisLap[car] > totalCheckpointCount / 2 &&
                newCheckpointIdx == 0)
            {
                carLaps[car]++;
                int currentLap = carLaps[car];

                if (currentLap >= GameManager.LapsToComplete)
                    HandleCarFinished(car);

                checkPointsHitThisLap[car] = 1;

                DLogger.Log($"{car.name} completed lap {currentLap}", GameManager, LogCategory);
                OnCarLapped?.Invoke(car, currentLap);
            }
        }

        private void TickRaceFinishTimer()
        {
            if (!raceFinishTimerRunning || raceFinished) return;

            if (Time.time - raceFinishStartTime >= raceFinishTimeout)
                FinishRace();
        }

        private void HandleCarFinished(Car car)
        {
            if (raceFinished || carFinishTimes.ContainsKey(car)) return;

            float finishTime = Time.timeSinceLevelLoad;
            carFinishTimes[car] = finishTime;

            if (!finishOrderSnapshot.Contains(car))
                finishOrderSnapshot.Add(car);

            OnCarFinishedRace?.Invoke(car, finishOrderSnapshot.Count);

            if(car == GameManager.PlayerCar)
            {
                var aiTakeover = new AICarController(waypointBuilder.Centerline, waypointBuilder.LeftEdge, waypointBuilder.RightEdge);
                car.GetCarComponent<CarInputComponent>().SetInputs(aiTakeover);
            }

            if (!raceFinishTimerRunning)
            {
                raceFinishTimerRunning = true;
                raceFinishStartTime = Time.time;
            }

            if (carFinishTimes.Count == GameManager.AllCars.Count)
                FinishRace();
        }

        private void FinishRace()
        {
            if (raceFinished) return;
            raceFinished = true;

            var results = new List<(Car car, float time)>();

            foreach (var car in finishOrderSnapshot)
                results.Add((car, carFinishTimes[car]));

            foreach (var car in carPlacements)
            {
                if (carFinishTimes.ContainsKey(car)) continue;
                results.Add((car, -1f));
            }

            OnRaceFinished?.Invoke(results);
        }
    }

    public partial class GameState // GameState.CarRecovery.cs
    {
        private readonly Dictionary<Car, float> carUpsideDownTimers = new();

        private float minTrackY;

        private void SetupCarRecovery()
        {
            carUpsideDownTimers.Clear();

            minTrackY = bezierTrackGenerator.GetComponent<MeshRenderer>() != null
                ? bezierTrackGenerator.GetComponent<MeshRenderer>().bounds.min.y
                : -10f;
        }

        private void TickCarRecovery()
        {
            foreach (Car car in GameManager.AllCars)
            {
                bool upsideDown = Vector3.Dot(car.transform.up, Vector3.down) > 0.7f;
                if (!carUpsideDownTimers.TryGetValue(car, out float timer))
                    timer = 0f;

                if (upsideDown)
                {
                    timer += Time.fixedDeltaTime;

                    if (timer >= 3f)
                    {
                        ResetCarRotation(car);
                        timer = 0f;
                    }
                }
                else
                {
                    timer = 0f;
                }

                carUpsideDownTimers[car] = timer;

                if (car.transform.position.y < minTrackY - 10f)
                {
                    RespawnCarAtCheckpoint(car);
                }
            }
        }

        public void ResetCarRotation(Car car)
        {
            Rigidbody rb = car.Rigidbody;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.ResetInertiaTensor();

            float yaw = car.transform.eulerAngles.y;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
        }

        public void RespawnCarAtCheckpoint(Car car)
        {
            if (GameManager.CheckpointManager == null) return;

            GameManager.CheckpointManager.GetLastCheckpointPose(car, out _, out Quaternion rot);
            Vector3 safePos = FindSafeLocationOnTrack(car);

            Rigidbody rb = car.Rigidbody;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = safePos;
                rb.rotation = rot;
                rb.ResetInertiaTensor();
            }
            else
            {
                car.transform.SetPositionAndRotation(safePos, rot);
            }
        }

        private Vector3 FindSafeLocationOnTrack(Car car)
        {
            if (waypointBuilder == null || waypointBuilder.Centerline.Count == 0)
                return car.transform.position;

            GameManager.CheckpointManager.GetLastCheckpointPose(car, out Vector3 cpPos, out _);

            int startIdx = 0;
            float minD = float.MaxValue;
            for (int i = 0; i < waypointBuilder.Centerline.Count; i++)
            {
                float d = Vector3.Distance(waypointBuilder.Centerline[i], cpPos);
                if (d < minD) { minD = d; startIdx = i; }
            }

            for (int i = 0; i < 12; i++)
            {
                int idx = (startIdx - (i * 2) + waypointBuilder.Centerline.Count) % waypointBuilder.Centerline.Count;

                Vector3 leftLane = Vector3.Lerp(waypointBuilder.Centerline[idx], waypointBuilder.LeftEdge[idx], 0.5f);
                if (IsPosSafe(leftLane, car)) return leftLane + Vector3.up * 0.2f;

                Vector3 rightLane = Vector3.Lerp(waypointBuilder.Centerline[idx], waypointBuilder.RightEdge[idx], 0.5f);
                if (IsPosSafe(rightLane, car)) return rightLane + Vector3.up * 0.2f;
            }

            return cpPos + Vector3.up * 0.2f;
        }

        private bool IsPosSafe(Vector3 pos, Car self)
        {
            foreach (var other in GameManager.AllCars)
            {
                if (other == self || other == null) continue;

                Vector3 toPos = pos - other.transform.position;
                float dist = toPos.magnitude;

                if (dist < 6f) return false;

                Vector3 forward = other.Rigidbody != null && other.Rigidbody.linearVelocity.magnitude > 1f
                    ? other.Rigidbody.linearVelocity.normalized
                    : other.transform.forward;

                if (dist < 30f && Vector3.Dot(forward, toPos.normalized) > 0.75f)
                    return false;
            }
            return true;
        }
    }
}
