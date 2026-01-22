using NoSlimes.Logging;
using RacingGame._Game.Scripts.PCG;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace RacingGame
{
    public partial class GameState : GameManagerState
    {
        public GameState(GameManager gameManager, DLogCategory logCategory, bool autoSpawnCars) : base(gameManager, logCategory) => this.autoSpawnCars = autoSpawnCars;

        private bool hasEnteredBefore = false;
        private readonly bool autoSpawnCars = true;

        private TrackWaypointBuilder waypointBuilder;

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

            waypointBuilder = Object.FindAnyObjectByType<TrackWaypointBuilder>();
        }

        public override void Exit()
        {
            base.Exit();

            GameManager.CheckpointManager.OnCarPassedCheckpoint -= OnCarPassedCheckpoint;
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

            TickCarRecovery();
        }
    }

    public partial class GameState // GameState.Spawning.cs
    {
        private void SpawnCars()
        {
            GameManager.StartCoroutine(SpawnCoroutine());
            SetupLaps();
        }

        private IEnumerator SpawnCoroutine()
        {
            yield return null; // Yield one frame to ensure everything is initialized
            GameManager.CarSpawner.SpawnCars();
        }
    }

    public partial class GameState // GameState.LapTracking.cs
    {
        private readonly Dictionary<Car, int> carLaps = new();
        private readonly Dictionary<Car, int> checkPointsHitThisLap = new();

        private void SetupLaps()
        {
            carLaps.Clear();

            foreach (var car in GameManager.AllCars)
                carLaps[car] = 0;
        }

        private void OnCarPassedCheckpoint(Car car, int newCheckpointIdx, int lastCheckpointIdx, int totalCheckpointCount)
        {
            if (!carLaps.ContainsKey(car)) carLaps[car] = 0;
            if (!checkPointsHitThisLap.ContainsKey(car)) checkPointsHitThisLap[car] = 1;

            checkPointsHitThisLap[car]++;

            if (lastCheckpointIdx >= totalCheckpointCount - 3 && checkPointsHitThisLap[car] > totalCheckpointCount / 2 && newCheckpointIdx == 0)
            {
                carLaps[car]++;
                checkPointsHitThisLap[car] = 1;
                DLogger.Log($"{car.name} completed lap {carLaps[car]}", GameManager, LogCategory);
            }
        }
    }

    public partial class GameState // GameState.CarRecovery.cs
    {
        private readonly Dictionary<Car, float> carUpsideDownTimers = new();

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

                if (car.transform.position.y < -10f)
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
