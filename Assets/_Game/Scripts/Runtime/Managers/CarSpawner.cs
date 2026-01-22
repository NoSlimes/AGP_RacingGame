using NoSlimes.UnityUtils.Common;
using RacingGame._Game.Scripts.PCG;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RacingGame
{
    public class CarSpawner
    {
        private readonly TrackWaypointBuilder waypointBuilder;
        private readonly Car carPrefab;
        private readonly int carCount;
        private readonly bool spawnPlayer;

        private readonly List<Car> spawnedCars = new();
        private readonly (Vector3 spawnPoint, Vector3 trackForward)[] spawnPositions;

        public Car PlayerCar { get; private set; }
        public IReadOnlyList<Car> SpawnedCars => spawnedCars;
        public IReadOnlyList<(Vector3 spawnPoint, Vector3 trackForward)> SpawnPositions => spawnPositions;

        public event Action OnCarsSpawned;

        public CarSpawner(TrackWaypointBuilder waypointBuilder, Car carPrefab, int carCount, bool spawnPlayer = true)
        {
            this.waypointBuilder = waypointBuilder;
            this.carPrefab = carPrefab;
            this.carCount = carCount;
            this.spawnPlayer = spawnPlayer;

            spawnPositions = new (Vector3, Vector3)[carCount];
            PrepareSpawnPositions();
        }

        public Car[] SpawnCars()
        {
            foreach ((Vector3 spawnPoint, Vector3 trackForward) in spawnPositions)
            {
                var forwardRotation = Quaternion.LookRotation(trackForward, Vector3.up);

                Car newCar = UnityEngine.Object.Instantiate(carPrefab, spawnPoint, forwardRotation);
                spawnedCars.Add(newCar);
            }

            var playerCarIndex = UnityEngine.Random.Range(0, spawnedCars.Count);
            for (int i = 0; i < spawnedCars.Count; i++)
            {
                Car car = spawnedCars[i];
                if (car.TryGetComponent(out CarInputComponent inputComp))
                {
                    if (i == playerCarIndex && spawnPlayer)
                    {
                        inputComp.SetInputs(new PlayerCarInputs());
                        PlayerCar = car;
                    }
                    else
                    {
                        List<Vector3> centerLine = waypointBuilder.Centerline;
                        List<Vector3> leftEdge = waypointBuilder.LeftEdge;
                        List<Vector3> rightEdge = waypointBuilder.RightEdge;

                        var aiController = new AICarController(centerLine, leftEdge, rightEdge);
                        inputComp.SetInputs(aiController);
                    }
                }
            }

            if (PlayerCar)
                PlayerCar.SetName("Player");

            var aiCars = spawnedCars.Where(c => c != PlayerCar).ToList();

            for (int i = 0; i < aiCars.Count; i++)
            {
                Car car = aiCars[i];
                car.SetName($"AI {i}");
            }

            OnCarsSpawned?.Invoke();
            return spawnedCars.ToArray();
        }

        private void PrepareSpawnPositions()
        {
            for (int i = 0; i < carCount; i++)
            {
                spawnPositions[i] = GetSpawnPosition(i);
            }
        }
        private (Vector3, Vector3) GetSpawnPosition(int index)
        {
            List<Vector3> center = waypointBuilder.Centerline;
            List<Vector3> left = waypointBuilder.LeftEdge;
            List<Vector3> right = waypointBuilder.RightEdge;
            int totalWaypoints = center.Count;

            float distanceBetweenPoints = Vector3.Distance(center[^1], center[^2]);
            if (distanceBetweenPoints < 0.001f) distanceBetweenPoints = 0.1f;

            Bounds carBounds = carPrefab.transform.GetObjectBounds();
            float carLength = carBounds.size.z;
            float metersPerRow = carLength * 2.5f;
            int indicesPerRow = Mathf.Max(1, Mathf.CeilToInt(metersPerRow / distanceBetweenPoints));

            int row = index / 2;
            bool isLeftLane = index % 2 == 0;

            int backOffset = 1 + (row * indicesPerRow);
            backOffset = Mathf.Clamp(backOffset, 1, totalWaypoints - 1);

            Vector3 centerPt = center[^backOffset];
            Vector3 leftPt = left[^backOffset];
            Vector3 rightPt = right[^backOffset];

            Vector3 forward = backOffset > 1 ? (center[^(backOffset - 1)] - centerPt).normalized : (centerPt - center[^(backOffset + 1)]).normalized;
            if (forward == Vector3.zero) forward = carPrefab.transform.forward;

            Vector3 spawnPoint = isLeftLane
                ? Vector3.Lerp(centerPt, leftPt, 0.5f)
                : Vector3.Lerp(centerPt, rightPt, 0.5f);

            // Slight lift to prevent physics glitches with the track floor
            spawnPoint += Vector3.up * 0.1f;

            return (spawnPoint, forward);
        }

        public void ResetCars()
        {
            for (int i = 0; i < spawnedCars.Count; i++)
            {
                Car car = spawnedCars[i];
                (Vector3 spawnPoint, Vector3 trackForward) = spawnPositions[i];

                if (car.TryGetComponent(out Rigidbody rb))
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;

                    rb.ResetInertiaTensor();
                }

                car.transform.SetPositionAndRotation(
                    spawnPoint,
                    Quaternion.LookRotation(trackForward, Vector3.up));

                //car.Reset();
            }
        }
    }
}