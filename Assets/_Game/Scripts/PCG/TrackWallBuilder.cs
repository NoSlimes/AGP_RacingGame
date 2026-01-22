using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace RacingGame._Game.Scripts.PCG
{
    [ExecuteAlways]
    public class TrackWallBuilder : MonoBehaviour
    {
        [Header("References")] 
        public SplineContainer splineContainer;
        public TrackMeshExtruder meshExtruder;

        [Header("Prefab")]
        public GameObject wallSegmentPrefab;

        [Header("Placement")]
        [Min(0f)] public float wallPadding = 0.25f;
        public bool followSlope = false;

        [Header("Wall Shape")] 
        [Min(0.1f)] public float wallHeight = 1.2f;
        [Min(0.01f)] public float wallThickness = 0.25f;

        [Header("Sampling")] 
        [Min(0f)] public float overrideStepMeters = 0f;

        [Header("Build Output")] 
        public Transform wallRoot;
        public string wallRootName = "Walls";
        public bool clearOldOnBuild = true;

        [Header("Physics")] 
        public bool addColliders = true;
        public PhysicsMaterial wallPhysicsMaterial;
        public bool isTrigger = false;

        [Header("Build")] 
        public bool rebuild;

        void Reset()
        {
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();
            if (!meshExtruder) meshExtruder = GetComponent<TrackMeshExtruder>();
        }

        void OnEnable()
        {
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();
            if (!meshExtruder) meshExtruder = GetComponent<TrackMeshExtruder>();
        }

        void Update()
        {
            if (rebuild)
            {
                rebuild = false;
                Build();
            }
        }

        public void Build()
        {
            if (!splineContainer || splineContainer.Splines.Count == 0) return;
            if (!meshExtruder) meshExtruder = GetComponent<TrackMeshExtruder>();

            var spline = splineContainer.Splines[0];
            if (spline.Count < 2) return;

            EnsureRoot();

            if (clearOldOnBuild)
                ClearChildren(wallRoot);

            float length = SplineUtility.CalculateLength(spline, splineContainer.transform.localToWorldMatrix);
            if (length <= 0.01f) return;

            float step = (overrideStepMeters > 0f)
                ? overrideStepMeters
                : (meshExtruder ? meshExtruder.stepMeters : 1.5f);
            int ringCount = Mathf.Max(2, Mathf.CeilToInt(length / step));
            bool closed = spline.Closed;
            int effectiveRings = closed ? ringCount : ringCount + 1;

            // Compute how far out the wall should be from the centerline
            float halfRoad = (meshExtruder ? meshExtruder.roadWidth : 8f) * 0.5f;
            float curb = (meshExtruder && meshExtruder.generateCurbs) ? meshExtruder.curbWidth : 0f;
            float grass = (meshExtruder && meshExtruder.generateGrass) ? meshExtruder.grassWidth : 0f;

            // Outside grass:
            float wallOffset = halfRoad + curb + grass + wallPadding;

            Vector3 prevLeft = Vector3.zero, prevRight = Vector3.zero;
            bool hasPrev = false;

            for (int i = 0; i < effectiveRings; i++)
            {
                float t;
                if (closed) t = (i / (float)ringCount) % 1f;
                else t = Mathf.Clamp01(i / (float)ringCount);

                Vector3 pos = SplineUtility.EvaluatePosition(spline, t);
                Vector3 tan = SplineUtility.EvaluateTangent(spline, t);

                // Build a stable frame
                Vector3 up = followSlope ? SplineUtility.EvaluateUpVector(spline, t) : Vector3.up;

                // If tan is near zero, skip
                if (tan.sqrMagnitude < 0.000001f)
                    continue;

                Vector3 forward = tan.normalized;

                // Right vector from forward & up
                Vector3 right = Vector3.Cross(up, forward).normalized;
                if (right.sqrMagnitude < 0.000001f)
                    right = Vector3.right;

                Vector3 leftWallPoint = pos - right * wallOffset;
                Vector3 rightWallPoint = pos + right * wallOffset;

                if (hasPrev)
                {
                    SpawnSegment(prevLeft, leftWallPoint, up, "Wall_L_" + i);
                    SpawnSegment(prevRight, rightWallPoint, up, "Wall_R_" + i);
                }

                prevLeft = leftWallPoint;
                prevRight = rightWallPoint;
                hasPrev = true;
            }

            // Close the loop
            if (closed)
            {
                // sample t=0 for first points again
                float t0 = 0f;
                Vector3 pos0 = SplineUtility.EvaluatePosition(spline, t0);
                Vector3 tan0 = SplineUtility.EvaluateTangent(spline, t0);
                Vector3 up0 = followSlope ? SplineUtility.EvaluateUpVector(spline, t0) : Vector3.up;
                Vector3 forward0 = (tan0.sqrMagnitude < 0.000001f) ? Vector3.forward : tan0.normalized;
                Vector3 right0 = Vector3.Cross(up0, forward0).normalized;

                float halfRoad0 = (meshExtruder ? meshExtruder.roadWidth : 8f) * 0.5f;
                float curb0 = (meshExtruder && meshExtruder.generateCurbs) ? meshExtruder.curbWidth : 0f;
                float grass0 = (meshExtruder && meshExtruder.generateGrass) ? meshExtruder.grassWidth : 0f;
                float wallOffset0 = halfRoad0 + curb0 + grass0 + wallPadding;

                Vector3 firstLeft = pos0 - right0 * wallOffset0;
                Vector3 firstRight = pos0 + right0 * wallOffset0;

                SpawnSegment(prevLeft, firstLeft, up0, "Wall_L_Close");
                SpawnSegment(prevRight, firstRight, up0, "Wall_R_Close");
            }
        }

        void SpawnSegment(Vector3 a, Vector3 b, Vector3 up, string name)
        {
            Vector3 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.05f) return;

            Vector3 mid = (a + b) * 0.5f;

            // Wall uppright
            Vector3 forward = delta / len;
            Vector3 useUp = followSlope ? up : Vector3.up;

            Quaternion rot = Quaternion.LookRotation(forward, useUp);

            GameObject go;
            if (wallSegmentPrefab)
            {
                go = (Application.isPlaying)
                    ? Instantiate(wallSegmentPrefab, wallRoot)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(wallSegmentPrefab, wallRoot);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(wallRoot, true);

                // Remove the default collider if we’re going to add our own
                var defaultCol = go.GetComponent<Collider>();
                if (defaultCol) DestroyImmediate(defaultCol);
            }

            go.name = name;
            go.transform.position = mid;
            go.transform.rotation = rot;

            // Scale along local Z as length
            // (Assumes your prefab faces forward on Z; cubes are fine)
            go.transform.localScale = new Vector3(wallThickness, wallHeight, len);

            if (addColliders)
            {
                var box = go.GetComponent<BoxCollider>();
                if (!box) box = go.AddComponent<BoxCollider>();

                box.isTrigger = isTrigger;
                if (wallPhysicsMaterial) box.material = wallPhysicsMaterial;

                // Box collider size: match our scale in local space
                box.size = Vector3.one;
                box.center = new Vector3(0f, 0.5f, 0f); // lift so it sits on ground better
            }
        }

        void EnsureRoot()
        {
#if UNITY_EDITOR
            if (!wallRoot)
            {
                var existing = transform.Find(wallRootName);
                if (existing) wallRoot = existing;
            }
#endif
            if (!wallRoot)
            {
                var go = new GameObject(wallRootName);
                go.transform.SetParent(transform, false);
                wallRoot = go.transform;
            }
        }

        static void ClearChildren(Transform root)
        {
            if (!root) return;

            // destroy children safely in edit & play mode
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying) Object.DestroyImmediate(c);
                else Object.Destroy(c);
#else
                Object.Destroy(c);
#endif
            }
        }
    }
}