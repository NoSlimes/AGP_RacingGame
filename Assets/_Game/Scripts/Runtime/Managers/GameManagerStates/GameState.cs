using NoSlimes.Logging;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public partial class GameState : GameManagerState
    {
        public GameState(GameManager gameManager, DLogCategory logCategory, bool autoSpawnCars) : base(gameManager, logCategory) => this.autoSpawnCars = autoSpawnCars;

        private bool hasEnteredBefore = false;
        private readonly bool autoSpawnCars = true;

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

        private void SetupLaps()
        {
            carLaps.Clear();

            foreach (var car in GameManager.AllCars)
                carLaps[car] = 0;
        }

        private void OnCarPassedCheckpoint(Car car, int newCheckpointIdx, int lastCheckpointIdx, int totalCheckpointsCount)
        {
            if (!carLaps.ContainsKey(car)) carLaps[car] = 0;
            if (newCheckpointIdx == 0 && lastCheckpointIdx == totalCheckpointsCount - 1)
            {
                carLaps[car]++;
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

                // Kill-Y respawn
                if (car.transform.position.y < -10f)
                {
                    RespawnCarAtCheckpoint(car);
                }
            }

        }

        private void ResetCarRotation(Car car)
        {
            Rigidbody rb = car.Rigidbody;
            rb.angularVelocity = Vector3.zero;
            rb.ResetInertiaTensor();
            rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);

            float yaw = car.transform.eulerAngles.y;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void RespawnCarAtCheckpoint(Car car)
        {
            if (GameManager.CheckpointManager == null) return;
            GameManager.CheckpointManager.GetLastCheckpointPose(out Vector3 pos, out Quaternion rot);

            Rigidbody rb = car.Rigidbody;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = pos;
                rb.rotation = rot;
                rb.ResetInertiaTensor();
                return;
            }

            car.transform.SetPositionAndRotation(pos, rot);
        }
    }
}
