using UnityEngine;
using System.Collections.Generic;
using System;

namespace RacingGame._Game.Scripts.PCG
{
    [ExecuteAlways]
    public class CheckpointManager : MonoBehaviour
    {
        [System.Serializable]
        public class CarCheckpointState
        {
            public int lastCheckpointIndex = 0;
            public Vector3 lastRespawnPos;
            public Quaternion lastRespawnRot;
            public float lastWrongCheckpointTime = -999f;
        }

        public float wrongCheckpointCooldown = 0.5f;
        private readonly Dictionary<int, CarCheckpointState> _stateByCar = new();

        [Header("PCG Input")]
        public TrackWaypointBuilder waypointBuilder;
        public TrackMeshExtruder meshExtruder;

        [Header("Checkpoint Generation")]
        [Min(1)]
        public int everyNWaypoints = 10;
        public float gateWidthPadding = 1.0f;
        public float gateHeight = 4.0f;
        public float gateLength = 2.5f;
        public float yOffset = 0.2f;

        [Header("Respawn")]
        public float respawnUpOffset = 0.6f;
        public bool flattenRespawnRotation = true;

        [Header("Ordering / Anti-cheat")]
        public bool enforceForwardProgress = true;

        [Header("Anti-Backwards")]
        public bool respawnIfGoingBackwards = true;

        [Header("Debug")]
        public bool drawGizmos = true;
        public float gizmoSphereRadius = 0.35f;

        private Transform _checkpointRoot;
        private readonly List<CheckpointGate> _gates = new();

        public delegate void CheckpointPassedDelegate(Car car, int newCheckpointIndex, int lastCheckpointIndex, int checkpointsCount);
        public event CheckpointPassedDelegate OnCarPassedCheckpoint;

        private void OnEnable()
        {
            AutoFindRefs();
        }

        private void Start()
        {
            if (Application.isPlaying)
                if (_gates.Count == 0)
                    BuildCheckpoints();
        }

        private void AutoFindRefs()
        {
            if (!waypointBuilder) waypointBuilder = FindAnyObjectByType<TrackWaypointBuilder>();
            if (!meshExtruder) meshExtruder = FindAnyObjectByType<TrackMeshExtruder>();

            if (_checkpointRoot == null)
            {
                var existing = transform.Find("Checkpoints");
                if (existing != null) _checkpointRoot = existing;
                else
                {
                    var go = new GameObject("Checkpoints");
                    go.transform.SetParent(transform, false);
                    _checkpointRoot = go.transform;
                }
            }
        }

        public void BuildCheckpoints()
        {
            AutoFindRefs();

            if (!waypointBuilder)
            {
                Debug.LogWarning("[CheckpointManager] Missing TrackWaypointBuilder reference.");
                return;
            }

            // Prefer spawned waypoint transforms if they exist, otherwise use sampled positions list
            var waypoints = waypointBuilder.Waypoints;
            var positions = waypointBuilder.WaypointPositions;

            int count = 0;
            if (waypoints != null && waypoints.Count > 1) count = waypoints.Count;
            else if (positions != null && positions.Count > 1) count = positions.Count;

            if (count < 2)
            {
                Debug.LogWarning("[CheckpointManager] Not enough waypoints to build checkpoints.");
                return;
            }

            ClearOldCheckpoints();

            // Gate width sizing
            float roadWidth = 8f;
            float curbWidth = 0f;

            if (meshExtruder)
            {
                roadWidth = meshExtruder.roadWidth;
                curbWidth = meshExtruder.generateCurbs ? meshExtruder.curbWidth : 0f;
            }

            float gateWidth = roadWidth + (curbWidth * 2f) + gateWidthPadding;

            // Build gates
            int gateIndex = 0;

            // Always make checkpoint 0 at waypoint 0
            CreateGateAtWaypoint(0, gateIndex++, gateWidth, count, waypoints, positions);

            // Then every N waypoints
            for (int i = everyNWaypoints; i < count; i += everyNWaypoints)
            {
                //Prevent a checkpoint from spawning too close to the end.
                if ((i - count) * 1.5f > everyNWaypoints)
                    continue;

                CreateGateAtWaypoint(i, gateIndex++, gateWidth, count, waypoints, positions);
            }

            // ------------------
            //
            // Creating a checkpoint so close to the finish line made the order all messed up and made it miss check points.
            // Commenting out for now if you want to find a better solution in the future.
            //
            // ------------------

            //int nearEnd = Mathf.Max(0, count - Mathf.Max(2, everyNWaypoints));
            //if (nearEnd > 0 && (nearEnd % everyNWaypoints) != 0)
            //{
            //    CreateGateAtWaypoint(nearEnd, gateIndex++, gateWidth, count, waypoints, positions);
            //}

            Debug.Log($"[CheckpointManager] Built {_gates.Count} checkpoints from {count} waypoints.");
        }

        private CarCheckpointState GetState(Car car)
        {
            int id = car.GetInstanceID();
            if (!_stateByCar.TryGetValue(id, out var s))
            {
                s = new CarCheckpointState();
                _stateByCar[id] = s;

                // initialize to checkpoint 0
                if (_gates.Count > 0 && _gates[0] != null)
                {
                    var t = _gates[0].transform;
                    s.lastRespawnPos = t.position + Vector3.up * respawnUpOffset;
                    s.lastRespawnRot = t.rotation;
                }
                else
                {
                    s.lastRespawnPos = car.transform.position;
                    s.lastRespawnRot = car.transform.rotation;
                }
            }

            return s;
        }

        private void CreateGateAtWaypoint(
            int waypointIndex,
            int checkpointIndex,
            float gateWidth,
            int totalWaypointCount,
            List<Transform> waypointTransforms,
            List<Vector3> waypointPositions)
        {
            Vector3 p0 = GetWaypointWorldPos(waypointIndex, waypointTransforms, waypointPositions);

            int nextWp = (waypointIndex + 1) % totalWaypointCount;
            Vector3 p1 = GetWaypointWorldPos(nextWp, waypointTransforms, waypointPositions);

            Vector3 forward = (p1 - p0);
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            var go = new GameObject($"CP_{checkpointIndex:000}_WP_{waypointIndex:000}");
            go.transform.SetParent(_checkpointRoot, false);

            go.transform.position = p0 + Vector3.up * yOffset;
            go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(gateWidth, gateHeight, gateLength);
            box.center = new Vector3(0f, gateHeight * 0.5f, 0f);

            var gate = go.AddComponent<CheckpointGate>();
            gate.manager = this;
            gate.checkpointIndex = checkpointIndex;

            _gates.Add(gate);
        }

        private Vector3 GetWaypointWorldPos(int i, List<Transform> wps, List<Vector3> pos)
        {
            if (wps != null && wps.Count > i && wps[i] != null)
                return wps[i].position;

            return pos[i];
        }

        private void ClearOldCheckpoints()
        {
            _gates.Clear();

            if (_checkpointRoot == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                for (int i = _checkpointRoot.childCount - 1; i >= 0; i--)
                    DestroyImmediate(_checkpointRoot.GetChild(i).gameObject);
                return;
            }
#endif
            for (int i = _checkpointRoot.childCount - 1; i >= 0; i--)
                Destroy(_checkpointRoot.GetChild(i).gameObject);
        }

        public void NotifyCheckpointPassed(int checkpointIndex, Car car)
        {
            if (car == null) return;
            if (_gates.Count <= 0) return;

            var s = GetState(car);

            int gateCount = _gates.Count;
            int prev = s.lastCheckpointIndex;
            int expected = (prev + 1) % gateCount;

            if (enforceForwardProgress)
            {
                if (checkpointIndex != expected && checkpointIndex != prev)
                {
                    // Prevent going backwards
                    if (respawnIfGoingBackwards && Time.time - s.lastWrongCheckpointTime > wrongCheckpointCooldown)
                    {
                        // Respawn them to the last valid checkpoint
                        s.lastWrongCheckpointTime = Time.time;

                        GameManager.Instance.StateMachine.GetState<GameState>().RespawnCarAtCheckpoint(car); // Ugly fix to fit with centralized reset system

                        //// Respawn only specific car
                        //var recovery = who.GetComponent<CarTopleRecovery>();
                        //if (recovery != null)
                        //    recovery.TryRespawn();
                    }

                    return;
                }
            }

            s.lastCheckpointIndex = checkpointIndex;

            // Update respawn pos
            var t = _gates[checkpointIndex].transform;
            s.lastRespawnPos = t.position + Vector3.up * respawnUpOffset;
            s.lastRespawnRot = t.rotation;

            if (flattenRespawnRotation)
            {
                var fwd = s.lastRespawnRot * Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                s.lastRespawnRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }

            OnCarPassedCheckpoint?.Invoke(car, checkpointIndex, prev, gateCount);

            // Debug
            Debug.Log($"[CheckpointManager] PASS CP {checkpointIndex} (prev {prev}, expected {expected})");

        }

        public void GetLastCheckpointPose(Car car, out Vector3 pos, out Quaternion rot)
        {
            var s = GetState(car);
            pos = s.lastRespawnPos;
            rot = s.lastRespawnRot;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (_checkpointRoot == null) return;

            Gizmos.color = Color.cyan;

            for (int i = 0; i < _checkpointRoot.childCount; i++)
            {
                var t = _checkpointRoot.GetChild(i);
                Gizmos.DrawSphere(t.position, gizmoSphereRadius);
                Gizmos.DrawLine(t.position, t.position + t.forward * 2f);
            }

            Gizmos.color = Color.white;
        }
    }
}