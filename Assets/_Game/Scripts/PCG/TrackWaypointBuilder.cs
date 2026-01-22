using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace RacingGame._Game.Scripts.PCG
{
    [ExecuteAlways]
    public class TrackWaypointBuilder : MonoBehaviour
    {
        [Header("Input")]
        public SplineContainer splineContainer;

        [Header("Waypoint Output")]
        public Transform waypointRoot;
        public string waypointNamePrefix = "WP_";
        public bool spawnGameObjects = true;
        public bool clearOldOnBuild = true;

        [Header("Sampling")]
        [Min(0.25f)] public float spacingMeters = 6f;
        public float heightOffset = 0.5f;
        public bool alignRotationToTangent = true;

        [Header("Speed Hints")]
        [Tooltip("If true, we compute curvature/radius and a recommended speed for each waypoint.")]
        public bool computeSpeedHints = true;
        [Tooltip("Treat turns using planar (flattened) direction to avoid hills affecting turn sharpness.")]
        public bool usePlanarTurn = true;
        [Tooltip("Friction coefficient used for corner speed approximation. Arcade: 1.2-2.0, Sim: 0.8-1.2")]
        [Range(0.2f, 3.0f)]
        public float frictionMu = 1.4f;
        [Tooltip("Multiplier on computed corner speed. Lower = more cautious AI.")]
        [Range(0.3f, 2.0f)]
        public float speedMultiplier = 1.0f;
        [Tooltip("Minimum recommended speed (m/s).")]
        [Min(0f)]
        public float minRecommendedSpeed = 4f;
        [Tooltip("Maximum recommended speed (m/s).")]
        [Min(0f)]
        public float maxRecommendedSpeed = 60f;
        [Tooltip("Anything with turn angle below this is treated as straight-ish.")]
        [Range(0f, 30f)]
        public float straightAngleThresholdDeg = 3f;

        [Header("Brake/Accel Zones")]
        [Tooltip("How many waypoints ahead to consider when deciding if we need to brake now.")]
        [Min(1)]
        public int lookAheadWaypoints = 8;
        [Tooltip("If upcoming recommended speed is lower than current by this amount (m/s), mark a brake zone.")]
        [Min(0f)]
        public float brakeDeltaThreshold = 6f;
        [Tooltip("How many waypoints BEFORE a slow corner to mark as Brake zone.")]
        [Min(0)]
        public int brakeLeadWaypoints = 4;
        [Tooltip("How many waypoints AFTER a slow corner to mark as Accelerate zone.")]
        [Min(0)]
        public int accelTailWaypoints = 4;

        [Header("Reference")]
        public TrackMeshExtruder meshExtruder;

        [Header("Track Lines")]
        public List<Vector3> Centerline = new();
        public List<Vector3> LeftEdge = new();
        public List<Vector3> RightEdge = new();

        [Header("Debug")]
        public bool drawGizmos = true;
        public float gizmoRadius = 0.35f;

        [Header("Build")]
        public bool rebuild;

        public List<Transform> Waypoints { get; private set; } = new List<Transform>();
        public List<Vector3> WaypointPositions { get; private set; } = new List<Vector3>();

        // Computed hints (parallel to WaypointPositions)
        public List<float> RecommendedSpeeds { get; private set; } = new List<float>();
        public List<float> Radii { get; private set; } = new List<float>();
        public List<float> Curvatures { get; private set; } = new List<float>();
        public List<float> TurnAnglesDeg { get; private set; } = new List<float>();
        public List<WaypointZoneType> Zones { get; private set; } = new List<WaypointZoneType>();

        private void Reset()
        {
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();
        }

        private void OnEnable()
        {
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();
            if (!meshExtruder) meshExtruder = FindAnyObjectByType<TrackMeshExtruder>();
            if (!waypointRoot) waypointRoot = transform;
            Build();
        }

        private void Update()
        {
            if (rebuild)
            {
                rebuild = false;
                Build();
            }
        }

        public void Build()
        {
            Waypoints.Clear();
            WaypointPositions.Clear();

            RecommendedSpeeds.Clear();
            Radii.Clear();
            Curvatures.Clear();
            TurnAnglesDeg.Clear();
            Zones.Clear();

            Centerline.Clear();
            LeftEdge.Clear();
            RightEdge.Clear();

            if (!splineContainer || splineContainer.Splines.Count == 0)
                return;

            Spline spline = splineContainer.Splines[0];
            if (spline.Count < 2)
                return;

            bool closed = spline.Closed;

            float length = SplineUtility.CalculateLength(spline, splineContainer.transform.localToWorldMatrix);
            if (length <= 0.01f)
                return;

            int count = Mathf.Max(2, Mathf.FloorToInt(length / spacingMeters));
            int sampleCount = closed ? count : (count + 1);

            if (!waypointRoot) waypointRoot = transform;

            // Clear old WPs
            if (spawnGameObjects && clearOldOnBuild)
            {
                var toDelete = new List<Transform>();
                for (int i = 0; i < waypointRoot.childCount; i++)
                {
                    Transform c = waypointRoot.GetChild(i);
                    if (c.name.StartsWith(waypointNamePrefix))
                        toDelete.Add(c);
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    foreach (Transform t in toDelete) DestroyImmediate(t.gameObject);
                }
                else
#endif
                {
                    foreach (Transform t in toDelete) Destroy(t.gameObject);
                }
            }

            // Positions + create waypoint objects
            for (int i = 0; i < sampleCount; i++)
            {
                float t = closed ? i / (float)sampleCount : (sampleCount <= 1) ? 0f : i / (float)(sampleCount - 1);
                Vector3 localPos = SplineUtility.EvaluatePosition(spline, t);
                Vector3 localTan = SplineUtility.EvaluateTangent(spline, t);

                Vector3 worldPos = splineContainer.transform.TransformPoint(localPos);
                Vector3 worldTan = splineContainer.transform.TransformDirection(localTan).normalized;

                worldPos += Vector3.up * heightOffset;

                WaypointPositions.Add(worldPos);

                if (spawnGameObjects)
                {
                    var go = new GameObject($"{waypointNamePrefix}{i:000}");
                    go.transform.SetParent(waypointRoot, worldPositionStays: true);
                    go.transform.position = worldPos;

                    if (alignRotationToTangent && worldTan.sqrMagnitude > 0.0001f)
                        go.transform.rotation = Quaternion.LookRotation(FlattenIfNeeded(worldTan), Vector3.up);

                    Waypoints.Add(go.transform);
                }

                // Center, right and left line
                float roadHalfWidth = meshExtruder.roadWidth * 0.5f;

                Centerline.Add(worldPos);

                Vector3 right = Vector3.Cross(Vector3.up, localTan).normalized;
                Vector3 left = -right;

                LeftEdge.Add(worldPos + (left * roadHalfWidth));
                RightEdge.Add(worldPos + (right * roadHalfWidth));
            }

            int n = WaypointPositions.Count;
            if (n < 3)
                return;

            for (int i = 0; i < n; i++)
            {
                RecommendedSpeeds.Add(maxRecommendedSpeed);
                Radii.Add(float.PositiveInfinity);
                Curvatures.Add(0f);
                TurnAnglesDeg.Add(0f);
                Zones.Add(WaypointZoneType.Cruise);
            }

            if (!computeSpeedHints)
                return;

            // Compute curvature/radius/turn angle + recommended speed per WP
            // Physics-ish corner speed: v = sqrt(mu * g * R)
            const float g = 9.81f;

            for (int i = 0; i < n; i++)
            {
                int iPrev = closed ? (i - 1 + n) % n : Mathf.Max(0, i - 1);
                int iNext = closed ? (i + 1) % n : Mathf.Min(n - 1, i + 1);

                Vector3 pPrev = WaypointPositions[iPrev];
                Vector3 p = WaypointPositions[i];
                Vector3 pNext = WaypointPositions[iNext];

                Vector3 a = p - pPrev;
                Vector3 b = pNext - p;

                // Compute slope
                float slopeDeg = 0f;
                {
                    Vector3 dir = pNext - p;
                    float horiz = new Vector3(dir.x, 0f, dir.z).magnitude;
                    if (horiz > 0.0001f)
                        slopeDeg = Mathf.Atan2(dir.y, horiz) * Mathf.Rad2Deg;
                }

                Vector3 aDir = a.normalized;
                Vector3 bDir = b.normalized;

                if (usePlanarTurn)
                {
                    aDir = FlattenIfNeeded(aDir).normalized;
                    bDir = FlattenIfNeeded(bDir).normalized;
                }

                float angleRad = Vector3.Angle(aDir, bDir) * Mathf.Deg2Rad;
                float angleDeg = angleRad * Mathf.Rad2Deg;

                TurnAnglesDeg[i] = angleDeg;

                // If straight, max speed?
                if (angleDeg < straightAngleThresholdDeg || a.magnitude < 0.001f || b.magnitude < 0.001f)
                {
                    Radii[i] = float.PositiveInfinity;
                    Curvatures[i] = 0f;
                    RecommendedSpeeds[i] = maxRecommendedSpeed;
                    ApplyHintToWaypointGo(i, angleDeg, Curvatures[i], Radii[i], RecommendedSpeeds[i],
                        WaypointZoneType.Cruise, slopeDeg);
                    continue;
                }

                // Approximate radius using chord length and turn angle
                float avgSeg = 0.5f * (a.magnitude + b.magnitude);

                // curvature k ~= angle / arcLength
                float curvature = angleRad / Mathf.Max(0.0001f, avgSeg);
                float radius = (curvature > 0.000001f) ? (1f / curvature) : float.PositiveInfinity;

                Curvatures[i] = curvature;
                Radii[i] = radius;

                float v = Mathf.Sqrt(Mathf.Max(0f, frictionMu * g * radius)) * speedMultiplier;
                v = Mathf.Clamp(v, minRecommendedSpeed, maxRecommendedSpeed);

                RecommendedSpeeds[i] = v;

                ApplyHintToWaypointGo(i, angleDeg, curvature, radius, v, Zones[i], slopeDeg);
            }

            // Mark braking zones
            MarkBrakeAndAccelZones(closed);

            // Apply final zones to waypoint components
            for (int i = 0; i < n; i++)
            {
                ApplyHintToWaypointGo(i, TurnAnglesDeg[i], Curvatures[i], Radii[i], RecommendedSpeeds[i], Zones[i], 0f);
            }
        }

        private void MarkBrakeAndAccelZones(bool closed)
        {
            int n = WaypointPositions.Count;
            if (n < 3) return;

            // Detect “need to brake” at waypoint
            for (int i = 0; i < n; i++)
            {
                float current = RecommendedSpeeds[i];

                float minAhead = current;
                int minAheadIndex = i;

                for (int k = 1; k <= lookAheadWaypoints; k++)
                {
                    int j = i + k;
                    if (closed)
                        j %= n;
                    else if (j >= n)
                        break;

                    float v = RecommendedSpeeds[j];
                    if (v < minAhead)
                    {
                        minAhead = v;
                        minAheadIndex = j;
                    }
                }

                if (minAhead < current - brakeDeltaThreshold)
                {
                    // Mark waypoints as Brake
                    for (int lead = 0; lead <= brakeLeadWaypoints; lead++)
                    {
                        int b = i + lead;
                        if (closed)
                            b %= n;
                        else if (b >= n)
                            break;

                        Zones[b] = WaypointZoneType.Brake;
                    }

                    // Mark corner as Accelerate
                    for (int tail = 0; tail <= accelTailWaypoints; tail++)
                    {
                        int a = minAheadIndex + tail;
                        if (closed)
                            a %= n;
                        else if (a >= n)
                            break;

                        // Don’t overwrite brake zones
                        if (Zones[a] != WaypointZoneType.Brake)
                            Zones[a] = WaypointZoneType.Accelerate;
                    }
                }
            }

            // Everything else, Cruise
            for (int i = 0; i < n; i++)
            {
                if (Zones[i] != WaypointZoneType.Brake && Zones[i] != WaypointZoneType.Accelerate)
                    Zones[i] = WaypointZoneType.Cruise;
            }
        }

        private Vector3 FlattenIfNeeded(Vector3 v)
        {
            if (!usePlanarTurn) return v;
            v.y = 0f;
            return v.sqrMagnitude > 0.000001f ? v.normalized : Vector3.forward;
        }

        private void ApplyHintToWaypointGo(int index, float angleDeg, float curvature, float radius, float recSpeed,
            WaypointZoneType zone, float slopeDeg)
        {
            if (!spawnGameObjects) return;
            if (index < 0 || index >= Waypoints.Count) return;

            Transform t = Waypoints[index];
            if (!t) return;

            WaypointSpeedHint hint = t.GetComponent<WaypointSpeedHint>();
            if (!hint) hint = t.gameObject.AddComponent<WaypointSpeedHint>();

            hint.turnAngleDeg = angleDeg;
            hint.curvature = curvature;
            hint.radius = radius;
            hint.recommendedSpeed = recSpeed;
            hint.zone = zone;

            // Only write slope if provided
            if (Mathf.Abs(slopeDeg) > 0.0001f)
                hint.slopeDeg = slopeDeg;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || WaypointPositions == null || WaypointPositions.Count == 0)
                return;

            for (int i = 0; i < WaypointPositions.Count; i++)
            {
                Color gizmoColor = Color.white;

                // Read from WaypointSpeedHint component
                if (spawnGameObjects && Waypoints != null && i < Waypoints.Count && Waypoints[i] != null)
                {
                    WaypointSpeedHint hint = Waypoints[i].GetComponent<WaypointSpeedHint>();
                    if (hint != null)
                    {
                        gizmoColor = ZoneToColor(hint.zone);
                    }
                }
                // Fallback, read from Zones list
                else if (Zones != null && i < Zones.Count)
                {
                    gizmoColor = ZoneToColor(Zones[i]);
                }

                Gizmos.color = gizmoColor;
                Gizmos.DrawSphere(WaypointPositions[i], gizmoRadius);

                // Draw connection line
                int next = i + 1;
                if (next < WaypointPositions.Count)
                {
                    Gizmos.color = gizmoColor * 0.75f;
                    Gizmos.DrawLine(WaypointPositions[i], WaypointPositions[next]);
                }
            }

            Gizmos.color = Color.white; // reset
        }

        private Color ZoneToColor(WaypointZoneType zone)
        {
            // Color for zones
            switch (zone)
            {
                case WaypointZoneType.Brake:
                    return Color.red;

                case WaypointZoneType.Accelerate:
                    return Color.green;

                case WaypointZoneType.Cruise:
                default:
                    return Color.white;
            }
        }
    }
}