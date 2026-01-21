using NoSlimes.UnityUtils.Common;
using RacingGame._Game.Scripts.PCG;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public partial class GameManager
    {
        private class CarSpawner
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
                foreach (var (spawnPoint, trackForward) in spawnPositions)
                {
                    var forwardRotation = Quaternion.LookRotation(trackForward, Vector3.up);

                    Car newCar = Instantiate(carPrefab, spawnPoint, forwardRotation);
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
                            var centerLine = waypointBuilder.Centerline;
                            var leftEdge = waypointBuilder.LeftEdge;
                            var rightEdge = waypointBuilder.RightEdge;

                            var aiController = new AICarController(centerLine, leftEdge, rightEdge);
                            inputComp.SetInputs(aiController);
                        }
                    }
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
                var center = waypointBuilder.Centerline;
                var left = waypointBuilder.LeftEdge;
                var right = waypointBuilder.RightEdge;

                float distanceBetweenPoints = Vector3.Distance(center[0], center[1]);
                if (distanceBetweenPoints < 0.001f) distanceBetweenPoints = 0.1f; 

                Bounds carBounds = carPrefab.transform.GetObjectBounds();
                float carLength = carBounds.size.z;

                float metersPerRow = carLength * 3.0f;

                int indicesPerRow = Mathf.CeilToInt(metersPerRow / distanceBetweenPoints);

                int row = index / 2;
                bool isLeftLane = index % 2 == 0;

                int startOffset = 10;
                int targetIndex = startOffset + (row * indicesPerRow);

                targetIndex = Mathf.Clamp(targetIndex, 0, center.Count - 1);
                int nextIndex = Mathf.Clamp(targetIndex + 1, 0, center.Count - 1);

                Vector3 centerPt = center[targetIndex];
                Vector3 leftPt = left[targetIndex];
                Vector3 rightPt = right[targetIndex];

                Vector3 forward = (center[nextIndex] - centerPt).normalized;
                if (forward == Vector3.zero) forward = carPrefab.transform.forward;

                Vector3 spawnPoint;
                if (isLeftLane)
                {
                    spawnPoint = Vector3.Lerp(centerPt, leftPt, 0.5f);
                }
                else
                {
                    spawnPoint = Vector3.Lerp(centerPt, rightPt, 0.5f);
                }

                spawnPoint += Vector3.up * 1.0f;

                return (spawnPoint, forward);
            }

            public void ResetCars()
            {
                for (int i = 0; i < spawnedCars.Count; i++)
                {
                    Car car = spawnedCars[i];
                    var (spawnPoint, trackForward) = spawnPositions[i];

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
}