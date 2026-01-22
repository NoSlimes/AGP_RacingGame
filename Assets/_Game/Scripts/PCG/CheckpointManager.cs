using UnityEngine;
using System.Collections.Generic;

namespace RacingGame._Game.Scripts.PCG
{
    [ExecuteAlways]
    public class CheckpointManager : MonoBehaviour
    {
        [Header("PCG Input")] 
        public TrackWaypointBuilder waypointBuilder;
        public TrackMeshExtruder meshExtruder;

        [Header("Checkpoint Generation")] [Min(1)]
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

        [Header("Runtime")] 
        public string playerTag = "Player";

        [Header("Debug")] 
        public bool drawGizmos = true;
        public float gizmoSphereRadius = 0.35f;

        // Runtime state
        public int LastCheckpointIndex { get; private set; } = 0;

        private Transform _player;
        private Transform _checkpointRoot;
        private readonly List<CheckpointGate> _gates = new();

        // Cached respawn pose
        private Vector3 _lastRespawnPos;
        private Quaternion _lastRespawnRot;

        private void OnEnable()
        {
            AutoFindRefs();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                AutoFindPlayer();
                if (_gates.Count == 0) BuildCheckpoints();
            }
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

        private void AutoFindPlayer()
        {
            if (_player != null) return;

            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) _player = p.transform;
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
                CreateGateAtWaypoint(i, gateIndex++, gateWidth, count, waypoints, positions);
            }
            
            int nearEnd = Mathf.Max(0, count - Mathf.Max(2, everyNWaypoints));
            if (nearEnd > 0 && (nearEnd % everyNWaypoints) != 0)
            {
                CreateGateAtWaypoint(nearEnd, gateIndex++, gateWidth, count, waypoints, positions);
            }

            // Reset state
            LastCheckpointIndex = 0;
            UpdateRespawnPoseFromGateIndex(LastCheckpointIndex, count, waypoints, positions);

            Debug.Log($"[CheckpointManager] Built {_gates.Count} checkpoints from {count} waypoints.");
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
            gate.playerTag = playerTag;

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
                    Object.DestroyImmediate(_checkpointRoot.GetChild(i).gameObject);
                return;
            }
#endif
            for (int i = _checkpointRoot.childCount - 1; i >= 0; i--)
                Destroy(_checkpointRoot.GetChild(i).gameObject);
        }

        public void NotifyCheckpointPassed(int checkpointIndex, Transform who)
        {
            // Cache player on first hit
            if (_player == null && who != null) _player = who;

            if (enforceForwardProgress)
            {
                // Only accept moving forward
                if (checkpointIndex < LastCheckpointIndex)
                    return;
            }

            LastCheckpointIndex = checkpointIndex;

            // Update respawn pose based on the checkpoint
            if (checkpointIndex >= 0 && checkpointIndex < _gates.Count && _gates[checkpointIndex] != null)
            {
                var t = _gates[checkpointIndex].transform;
                _lastRespawnPos = t.position + Vector3.up * respawnUpOffset;
                _lastRespawnRot = t.rotation;

                if (flattenRespawnRotation)
                {
                    var fwd = _lastRespawnRot * Vector3.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                    _lastRespawnRot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                }
            }

            // Debug
            Debug.Log($"[CheckpointManager] Player hit checkpoint {checkpointIndex}");
            Debug.Log($"[CheckpointManager] PASS CP {checkpointIndex} (prev {LastCheckpointIndex})");
        }

        private void UpdateRespawnPoseFromGateIndex(int checkpointIndex, int totalWaypointCount, List<Transform> wps,
            List<Vector3> pos)
        {
            // Fallback: if gates exist, use gate transform
            if (checkpointIndex >= 0 && checkpointIndex < _gates.Count && _gates[checkpointIndex] != null)
            {
                var t = _gates[checkpointIndex].transform;
                _lastRespawnPos = t.position + Vector3.up * respawnUpOffset;
                _lastRespawnRot = t.rotation;
                return;
            }

            // Otherwise, approximate from waypoint 0
            Vector3 p0 = GetWaypointWorldPos(0, wps, pos);
            Vector3 p1 = GetWaypointWorldPos(1 % totalWaypointCount, wps, pos);

            Vector3 forward = (p1 - p0);
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            _lastRespawnPos = p0 + Vector3.up * respawnUpOffset;
            _lastRespawnRot = Quaternion.LookRotation(forward, Vector3.up);
        }

        public void GetLastCheckpointPose(out Vector3 pos, out Quaternion rot)
        {
            pos = _lastRespawnPos;
            rot = _lastRespawnRot;
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