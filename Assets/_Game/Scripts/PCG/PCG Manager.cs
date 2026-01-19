using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RacingGame
{
    public class PCGManager : MonoBehaviour
    {
        // Track
        public int controlPointCount = 16;
        public float baseRadius = 60f;
        public float radiusNoise = 18f;
        public float smoothing = 0.5f;

        // Sampling
        public int samplesPerSegment = 16;
        public float roadWidth = 8f;

        // Road Mesh
        public Material roadMaterial;
        public float uvTiling = 4f;

        // Walls
        public bool spawnWalls = true;
        public GameObject wallPrefab;
        public float wallOffset = 0.5f;
        public float wallEveryMeters = 3f;

        // Waypoints
        public bool generateWaypoints = true;
        public float waypointEveryMeters = 6f;
        public Transform waypointParent;

        // Random
        public int seed = 1337;
        public bool randomizeSeed = false;

        // Outputs
        public List<Vector3> Centerline { get; private set; } = new();
        public List<Vector3> RightEdge { get; private set; } = new();
        public List<Vector3> LeftEdge { get; private set; } = new();

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        // Finder
        private Transform _generatedWaypointsRoot;
        private Transform _generatedWallsRoot;

        private void OnEnable()
        {
            EnsureComponents();
            if (Application.isPlaying)
                Generate();
        }

        private void OnValidate()
        {
            EnsureComponents();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Generate();
#endif
        }

        private void EnsureComponents()
        {
            if (!_meshFilter) _meshFilter = GetComponent<MeshFilter>();
            if (!_meshFilter) _meshFilter = gameObject.AddComponent<MeshFilter>();

            if (!_meshRenderer) _meshRenderer = GetComponent<MeshRenderer>();
            if (!_meshRenderer) _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            if (roadMaterial) _meshRenderer.sharedMaterial = roadMaterial;
        }

        public void Generate()
        {
            ClearPCG();

            _generatedWallsRoot = GetOrCreateChild(transform, "_Generated_Walls");

            Transform wpRootParent = waypointParent ? waypointParent : transform;
            _generatedWaypointsRoot = GetOrCreateChild(wpRootParent, "_Generated_Waypoints");

            int usedSeed = randomizeSeed ? Random.Range(int.MinValue / 2, int.MaxValue / 2) : seed;
            var rng = new System.Random(usedSeed);

            // Control points
            List<Vector3> cps = new();
            for (int i = 0; i < controlPointCount; i++)
            {
                float t = (float)i / controlPointCount;
                float ang = t * Mathf.PI * 2f;

                float noise = (float)(rng.NextDouble() * 2.0f - 1.0f) * radiusNoise;
                float r = Mathf.Max(8f, baseRadius + noise);

                Vector3 p = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                cps.Add(transform.TransformPoint(p));
            }

            // Smoothen
            cps = SmoothRing(cps, smoothing, 3);
            Centerline = SampleClosedCatmullRom(cps, samplesPerSegment);

            // Build Road
            BuildEdges(Centerline, roadWidth * 0.5f, RightEdge, LeftEdge);
            BuildRoadMesh(Centerline, RightEdge, LeftEdge);

            // TODO: Do we keep walls?
            if (spawnWalls && wallPrefab != null)
                SpawnWalls(RightEdge, LeftEdge);

            // For AI
            if (generateWaypoints)
                GenerateWaypointsObjects(Centerline);
        }

        private Transform GetOrCreateChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private List<Vector3> SmoothRing(List<Vector3> points, float amount, int iterations)
        {
            var pts = new List<Vector3>(points);
            for (int it = 0; it < iterations; it++)
            {
                var next = new List<Vector3>(pts.Count);
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 prev = pts[(i - 1 + pts.Count) % pts.Count];
                    Vector3 cur = pts[i];
                    Vector3 nxt = pts[(i + 1) % pts.Count];
                    Vector3 avg = (prev + cur + nxt) / 3f;
                    next.Add(Vector3.Lerp(cur, avg, amount));
                }

                pts = next;
            }

            return pts;
        }

        private List<Vector3> SampleClosedCatmullRom(List<Vector3> cps, int samplesPerSeg)
        {
            List<Vector3> result = new();
            int n = cps.Count;

            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = cps[(i - 1 + n) % n];
                Vector3 p1 = cps[i];
                Vector3 p2 = cps[(i + 1) % n];
                Vector3 p3 = cps[(i + 2) % n];

                for (int s = 0; s < samplesPerSeg; s++)
                {
                    float t = s / (float)samplesPerSeg;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            return result;
        }

        private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private void BuildEdges(List<Vector3> center, float halfWidth, List<Vector3> right, List<Vector3> left)
        {
            right.Clear();
            left.Clear();

            for (int i = 0; i < center.Count; i++)
            {
                Vector3 prev = center[(i - 1 + center.Count) % center.Count];
                Vector3 cur = center[i];
                Vector3 next = center[(i + 1) % center.Count];

                Vector3 tangent = (next - prev).normalized;
                Vector3 normal = Vector3.Cross(Vector3.up, tangent).normalized;

                right.Add(cur + normal * halfWidth);
                left.Add(cur - normal * halfWidth);
            }
        }

        private void BuildRoadMesh(List<Vector3> center, List<Vector3> right, List<Vector3> left)
        {
            Mesh mesh = new();
            mesh.name = "Road";

            int count = center.Count;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] triangles = new int[count * 6];

            float vCoord = 0f;

            for (int i = 0; i < count; i++)
            {
                int vi = i * 2;

                vertices[vi + 1] = transform.InverseTransformPoint(right[i]);
                vertices[vi + 0] = transform.InverseTransformPoint(left[i]);

                // UV:s
                if (i > 0)
                {
                    vCoord += Vector3.Distance(center[i - 1], center[i]) / uvTiling;
                }

                uv[vi + 1] = new Vector2(1f, vCoord);
                uv[vi + 0] = new Vector2(0f, vCoord);
            }

            for (int i = 0; i < count; i++)
            {
                int iNext = (i + 1) % count;

                int tri = i * 6;
                int v0 = i * 2;
                int v1 = i * 2 + 1;
                int v2 = iNext * 2;
                int v3 = iNext * 2 + 1;

                // double triangles per quad
                triangles[tri + 0] = v0;
                triangles[tri + 1] = v2;
                triangles[tri + 2] = v1;

                triangles[tri + 3] = v1;
                triangles[tri + 4] = v2;
                triangles[tri + 5] = v3;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _meshFilter.sharedMesh = mesh;
        }

        private void SpawnWalls(List<Vector3> right, List<Vector3> left)
        {
            float accR = 0f;
            float accL = 0f;

            for (int i = 1; i < right.Count; i++)
            {
                float dR = Vector3.Distance(right[i - 1], right[i]);
                accR += dR;

                if (accR >= wallEveryMeters)
                {
                    accR = 0f;
                    SpawnWallAt(right[i], right, i, +1);
                }

                float dL = Vector3.Distance(left[i - 1], left[i]);
                accL += dL;

                if (accL >= wallEveryMeters)
                {
                    accL = 0f;
                    SpawnWallAt(left[i], left, i, -1);
                }
            }
        }

        private void SpawnWallAt(Vector3 pos, List<Vector3> edge, int i, int sideSign)
        {
            Vector3 prev = edge[(i - 1 + edge.Count) % edge.Count];
            Vector3 next = edge[(i + 1) % edge.Count];
            Vector3 dir = (next - prev).normalized;

            // Offset from the road
            Vector3 outward = Vector3.Cross(dir, Vector3.up).normalized * sideSign;
            Vector3 p = pos + outward * wallOffset;

            var go = Instantiate(wallPrefab, p, Quaternion.LookRotation(dir, Vector3.up), _generatedWallsRoot);
            go.name = $"Wall_{sideSign}_{i}";
        }


        private void GenerateWaypointsObjects(List<Vector3> center)
        {
            Transform parent = waypointParent ? waypointParent : transform;
            Vector3 up = transform.up;

            float acc = 0f;
            int wpIndex = 0;

            for (int i = 1; i < center.Count; i++)
            {
                float d = Vector3.Distance(center[i - 1], center[i]);
                acc += d;

                if (acc >= waypointEveryMeters)
                {
                    acc = 0f;
                    Vector3 pos = center[i];
                    Vector3 forward = (center[(i + 1) % center.Count] - center[(i - 1 + center.Count) % center.Count])
                        .normalized;
                    forward = Vector3.ProjectOnPlane(transform.forward, up);

                    if (forward.sqrMagnitude < 0.0001f)
                    {
                        forward = Vector3.ProjectOnPlane(transform.forward, up);
                    }

                    var wp = new GameObject($"Waypoint_{wpIndex++}");
                    wp.transform.position = pos;
                    wp.transform.rotation = Quaternion.LookRotation(forward, up);
                    wp.transform.SetParent(_generatedWaypointsRoot, true);
                }
            }
        }

        private void ClearPCG()
        {
            // Clear waypoints
            Transform wpRootParent = waypointParent ? waypointParent : transform;
            var wpRoot = wpRootParent.Find("_Generated_Waypoints");
            if (wpRoot != null) DestroyChildren(wpRoot);

            // Clear walls
            var wallRoot = transform.Find("_Generated_Walls");
            if (wallRoot != null) DestroyChildren(wallRoot);
        }

        private void DestroyChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child.gameObject);
                }
                else
                {
                    Destroy(child.gameObject);
                }
#else
                Destroy(child.gameObject);
#endif
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Centerline == null || Centerline.Count < 2) return;

            Gizmos.color = Color.yellow;
            for (int i = 0; i < Centerline.Count; i++)
            {
                Vector3 a = Centerline[i];
                Vector3 b = Centerline[(i + 1) % Centerline.Count];
                Gizmos.DrawLine(a, b);
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < LeftEdge.Count; i++)
            {
                Gizmos.DrawLine(LeftEdge[i], RightEdge[i]);
            }
        }
    }
}