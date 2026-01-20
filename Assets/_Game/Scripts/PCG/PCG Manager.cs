using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RacingGame
{
    [ExecuteAlways]
    public class PCGManager : MonoBehaviour
    {
#if UNITY_EDITOR
        private bool _queuedRegen;
#endif

        public static PCGManager Instance;

        [Header("Track Layout (2D -> Bezier)")] [SerializeField]
        private int scatterPointCount = 28;

        [SerializeField] private Vector2 scatterSize = new Vector2(160f, 160f);
        [SerializeField] private float hullInsertJitter = 18f;
        [SerializeField] private int hullSubdivisionCount = 2;
        [SerializeField] private float postHullSmoothing = 0.12f;
        [SerializeField, Range(0f, 1f)] private float straightChance = 0.45f;

        [Header("Bezier Sampling")] [SerializeField, Range(0f, 1f)]
        private float bezierTension = 0.25f; // 0.2-0.35 best

        [SerializeField] private int bezierSamplesPerCorner = 14;

        [Header("Road (3D Mesh)")] [SerializeField]
        private float roadWidth = 8f;

        [SerializeField] private float roadThickness = 0.35f;
        [SerializeField] private Material roadMaterial;
        [SerializeField] private float uvTiling = 4f;

        [Header("Elevation (3D Hills)")] [SerializeField]
        private bool enableElevation = true;

        [SerializeField] private float elevationAmplitude = 6f; // meters
        [SerializeField] private float elevationNoiseScale = 0.02f; // lower = longer hills
        [SerializeField] private float maxSlopeDeg = 14f; // keep drivable
        [SerializeField] private float elevationTurnLimitDeg = 18f; // reduce height changes in sharp turns
        [SerializeField] private int elevationSmoothIterations = 2;
        [SerializeField] private int elevationSlopeRelaxPasses = 3; // makes loop seam smooth

        [Header("Walls (Mesh) - Always outside road")] [SerializeField]
        private bool buildWallsMesh = true;

        [SerializeField] private Material wallMaterial;
        [SerializeField] private float wallHeight = 1.2f;
        [SerializeField] private float wallThickness = 0.22f;
        [SerializeField] private float wallExtraOutward = 0.35f; // extra gap outside curb/edge
        [SerializeField] private bool addWallCollider = true;

        [Header("Waypoints (AI)")] [SerializeField]
        private bool generateWaypoints = true;

        [SerializeField] private float waypointEveryMeters = 6f;
        public Transform waypointParent; // optional organizer (if null, auto child created)

        [Header("Curbs (Mario Kart style, mesh)")] [SerializeField]
        private bool spawnCurbs = true;

        [SerializeField] private Material curbRedMaterial;
        [SerializeField] private Material curbWhiteMaterial;
        [SerializeField] private float curbWidth = 0.7f;
        [SerializeField] private float curbHeight = 0.08f;
        [SerializeField] private float curbOffsetOutward = 0.05f;
        [SerializeField] private float curbEveryMeters = 1.5f;
        [SerializeField] private float hardTurnAngleDeg = 18f; // lower => more curbs

        [Header("Random")] [SerializeField] private int seed = 1337;
        [SerializeField] private bool randomizeSeed = false;

        [Header("Outputs (Debug)")] public List<Vector3> Centerline { get; private set; } = new();
        public List<Vector3> RightEdge { get; private set; } = new();
        public List<Vector3> LeftEdge { get; private set; } = new();

        // Road mesh components on THIS object
        private MeshCollider _roadCollider;
        private MeshFilter _roadFilter;
        private MeshRenderer _roadRenderer;
        private Mesh _roadMesh;

        // Walls mesh object
        private GameObject _wallsGO;
        private MeshFilter _wallsFilter;
        private MeshRenderer _wallsRenderer;
        private MeshCollider _wallsCollider;
        private Mesh _wallsMesh;

        // Generated containers
        private Transform _generatedWaypointsRoot;

        // Curbs mesh object
        private GameObject _curbsGO;
        private MeshFilter _curbFilter;
        private MeshRenderer _curbRenderer;
        private Mesh _curbMesh;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

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
            if (Application.isPlaying) return;
            if (_queuedRegen) return;

            _queuedRegen = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _queuedRegen = false;
                if (this == null) return;
                Generate();
            };
#endif
        }

        private void EnsureComponents()
        {
            if (!_roadFilter) _roadFilter = GetComponent<MeshFilter>();
            if (!_roadFilter) _roadFilter = gameObject.AddComponent<MeshFilter>();

            if (!_roadRenderer) _roadRenderer = GetComponent<MeshRenderer>();
            if (!_roadRenderer) _roadRenderer = gameObject.AddComponent<MeshRenderer>();

            if (!_roadCollider) _roadCollider = GetComponent<MeshCollider>();
            if (!_roadCollider) _roadCollider = gameObject.AddComponent<MeshCollider>();

            if (roadMaterial) _roadRenderer.sharedMaterial = roadMaterial;

            if (_roadMesh == null)
            {
                _roadMesh = new Mesh { name = "RoadMesh_3D" };
                _roadMesh.MarkDynamic();
                _roadFilter.sharedMesh = _roadMesh;
            }

            EnsureWallsObject();
            EnsureCurbMeshObject();
            _generatedWaypointsRoot = GetOrCreateChild(transform, "_Generated_Waypoints");
        }

        public void Generate()
        {
            ClearPCG();

            int usedSeed = randomizeSeed ? Random.Range(int.MinValue / 2, int.MaxValue / 2) : seed;
            var rng = new System.Random(usedSeed);

            // 1) Layout control points (2D)
            List<Vector3> cps = BuildHullControlPoints(rng);

            // 2) Small smoothing (do NOT over-smooth)
            float autoSmooth = Mathf.Clamp01(postHullSmoothing - (hullInsertJitter / 220f));
            cps = SmoothRing(cps, autoSmooth, 1);

            // 3) Bezier centerline
            Centerline = SampleClosedBezierChain(cps, bezierTension, bezierSamplesPerCorner);

            // 4) Elevation pass (3D hills, slope-clamped, reduced in sharp turns)
            if (enableElevation)
                ApplyElevation(Centerline, rng);

            // 5) Build edges
            BuildEdges(Centerline, roadWidth * 0.5f, RightEdge, LeftEdge);

            // 6) Build 3D road mesh (top + bottom + sides)
            BuildRoadMesh3D(Centerline, RightEdge, LeftEdge);

            // 7) Curbs mesh (outside of hard turns)
            if (spawnCurbs && curbRedMaterial != null && curbWhiteMaterial != null)
                BuildCurbsMesh(Centerline, RightEdge, LeftEdge);
            else
                DisableCurbsObject();

            // 8) Walls mesh (always outside road)
            if (buildWallsMesh && wallMaterial != null)
                BuildWallsMesh(Centerline, RightEdge, LeftEdge);
            else
                DisableWallsObject();

            // 9) Waypoints
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

        // ---------------------------
        // Layout: scatter -> convex hull -> subdivide + jitter + straight bias
        // ---------------------------
        private List<Vector3> BuildHullControlPoints(System.Random rng)
        {
            List<Vector2> pts = new();
            for (int i = 0; i < scatterPointCount; i++)
            {
                float x = (float)(rng.NextDouble() * 2f - 1f) * scatterSize.x * 0.5f;
                float z = (float)(rng.NextDouble() * 2f - 1f) * scatterSize.y * 0.5f;
                pts.Add(new Vector2(x, z));
            }

            List<Vector2> hull = ConvexHull(pts);

            List<Vector3> cps = new();
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i];
                Vector2 b = hull[(i + 1) % hull.Count];

                bool makeStraight = rng.NextDouble() < straightChance;

                for (int s = 0; s <= hullSubdivisionCount; s++)
                {
                    float t = (hullSubdivisionCount == 0) ? 0f : s / (float)hullSubdivisionCount;
                    Vector2 p = Vector2.Lerp(a, b, t);

                    Vector2 edge = (b - a);
                    Vector2 n = new Vector2(-edge.y, edge.x);
                    if (n.sqrMagnitude > 0.0001f) n.Normalize();

                    // Weighted jitter: 0 at endpoints, max mid-edge
                    if (!makeStraight)
                    {
                        float w = Mathf.Sin(t * Mathf.PI);
                        float jitter = (float)(rng.NextDouble() * 2f - 1f) * hullInsertJitter * w;
                        p += n * jitter;
                    }

                    Vector3 wp = transform.TransformPoint(new Vector3(p.x, 0f, p.y));
                    cps.Add(wp);
                }
            }

            for (int i = cps.Count - 1; i > 0; i--)
                if ((cps[i] - cps[i - 1]).sqrMagnitude < 0.25f)
                    cps.RemoveAt(i);

            return cps;
        }

        private static List<Vector2> ConvexHull(List<Vector2> points)
        {
            var pts = new List<Vector2>(points);
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            float Cross(Vector2 o, Vector2 a, Vector2 b)
                => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

            List<Vector2> lower = new();
            foreach (var p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0f)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            List<Vector2> upper = new();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                var p = pts[i];
                while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0f)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);

            return lower;
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

        // ---------------------------
        // Bezier: closed cubic chain
        // ---------------------------
        private List<Vector3> SampleClosedBezierChain(List<Vector3> knots, float tension, int samplesPerCorner)
        {
            List<Vector3> result = new();
            int n = knots.Count;
            if (n < 3) return result;

            Vector3[] inH = new Vector3[n];
            Vector3[] outH = new Vector3[n];

            Vector3 up = transform.up;

            for (int i = 0; i < n; i++)
            {
                Vector3 pPrev = knots[(i - 1 + n) % n];
                Vector3 p = knots[i];
                Vector3 pNext = knots[(i + 1) % n];

                Vector3 vPrev = Vector3.ProjectOnPlane(p - pPrev, up);
                Vector3 vNext = Vector3.ProjectOnPlane(pNext - p, up);

                float lenPrev = vPrev.magnitude;
                float lenNext = vNext.magnitude;

                if (lenPrev < 0.0001f || lenNext < 0.0001f)
                {
                    inH[i] = p;
                    outH[i] = p;
                    continue;
                }

                Vector3 dirPrev = vPrev / lenPrev;
                Vector3 dirNext = vNext / lenNext;

                Vector3 tangent = Vector3.ProjectOnPlane(dirPrev + dirNext, up);
                if (tangent.sqrMagnitude < 0.0001f) tangent = dirNext;
                tangent.Normalize();

                float handleLenIn = lenPrev * tension;
                float handleLenOut = lenNext * tension;

                inH[i] = p - tangent * handleLenIn;
                outH[i] = p + tangent * handleLenOut;
            }

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;

                Vector3 p0 = knots[i];
                Vector3 p1 = outH[i];
                Vector3 p2 = inH[j];
                Vector3 p3 = knots[j];

                for (int s = 0; s < samplesPerCorner; s++)
                {
                    float t = s / (float)samplesPerCorner;
                    result.Add(CubicBezier(p0, p1, p2, p3, t));
                }
            }

            return result;
        }

        private Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return (uuu * p0) +
                   (3f * uu * t * p1) +
                   (3f * u * tt * p2) +
                   (ttt * p3);
        }

        // ---------------------------
        // Elevation: safe hills (Mario Kart)
        // ---------------------------
        private void ApplyElevation(List<Vector3> center, System.Random rng)
        {
            if (center == null || center.Count < 4) return;

            Vector3 up = transform.up;
            int n = center.Count;

            // Distance along loop
            float[] dist = new float[n];
            dist[0] = 0f;
            for (int i = 1; i < n; i++)
                dist[i] = dist[i - 1] + Vector3.Distance(center[i - 1], center[i]);

            float loopLen = dist[n - 1] + Vector3.Distance(center[n - 1], center[0]);
            float noiseOffset = (float)rng.NextDouble() * 1000f;

            float[] h = new float[n];

            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                int next = (i + 1) % n;

                Vector3 a = Vector3.ProjectOnPlane(center[i] - center[prev], up);
                Vector3 b = Vector3.ProjectOnPlane(center[next] - center[i], up);

                float angle = 0f;
                if (a.sqrMagnitude > 0.0001f && b.sqrMagnitude > 0.0001f)
                    angle = Vector3.Angle(a, b);

                // Gentle factor: 1 on straight, 0 on sharp turns
                float gentle = Mathf.InverseLerp(elevationTurnLimitDeg, 0f, angle);
                gentle = Mathf.Clamp01(gentle);

                float t = dist[i] / Mathf.Max(loopLen, 0.001f);
                float freq = 1f / Mathf.Max(elevationNoiseScale, 0.0001f);
                float noise = Mathf.PerlinNoise(noiseOffset + t * freq, noiseOffset + 0.37f);

                float centered = (noise * 2f - 1f);
                h[i] = centered * elevationAmplitude * gentle;
            }

            // Smooth heights
            for (int it = 0; it < elevationSmoothIterations; it++)
            {
                float[] tmp = new float[n];
                for (int i = 0; i < n; i++)
                {
                    int prev = (i - 1 + n) % n;
                    int next = (i + 1) % n;
                    tmp[i] = (h[prev] + h[i] + h[next]) / 3f;
                }

                h = tmp;
            }

            // Clamp slope around the LOOP (relaxation passes)
            float maxSlope = Mathf.Tan(maxSlopeDeg * Mathf.Deg2Rad);
            for (int pass = 0; pass < Mathf.Max(1, elevationSlopeRelaxPasses); pass++)
            {
                // forward
                for (int i = 1; i < n; i++)
                {
                    float seg = Vector3.Distance(center[i - 1], center[i]);
                    float maxDy = maxSlope * seg;
                    h[i] = Mathf.Clamp(h[i], h[i - 1] - maxDy, h[i - 1] + maxDy);
                }

                // wrap
                {
                    float seg = Vector3.Distance(center[n - 1], center[0]);
                    float maxDy = maxSlope * seg;
                    h[0] = Mathf.Clamp(h[0], h[n - 1] - maxDy, h[n - 1] + maxDy);
                }
                // backward
                for (int i = n - 2; i >= 0; i--)
                {
                    float seg = Vector3.Distance(center[i + 1], center[i]);
                    float maxDy = maxSlope * seg;
                    h[i] = Mathf.Clamp(h[i], h[i + 1] - maxDy, h[i + 1] + maxDy);
                }
            }

            // Apply heights along "up"
            for (int i = 0; i < n; i++)
                center[i] = center[i] + up * h[i];
        }

        // ---------------------------
        // Edges (stable width on hills)
        // ---------------------------
        private void BuildEdges(List<Vector3> center, float halfWidth, List<Vector3> right, List<Vector3> left)
        {
            right.Clear();
            left.Clear();
            if (center == null || center.Count < 4) return;

            Vector3 up = transform.up;

            Vector3 lastPlanarForward = Vector3.ProjectOnPlane(transform.forward, up);
            if (lastPlanarForward.sqrMagnitude < 0.0001f) lastPlanarForward = Vector3.forward;
            lastPlanarForward.Normalize();

            for (int i = 0; i < center.Count; i++)
            {
                Vector3 prev = center[(i - 1 + center.Count) % center.Count];
                Vector3 cur = center[i];
                Vector3 next = center[(i + 1) % center.Count];

                Vector3 planarForward = Vector3.ProjectOnPlane(next - prev, up);
                if (planarForward.sqrMagnitude < 0.0001f) planarForward = lastPlanarForward;
                planarForward.Normalize();
                lastPlanarForward = planarForward;

                Vector3 rightDir = Vector3.Cross(up, planarForward).normalized;

                right.Add(cur + rightDir * halfWidth);
                left.Add(cur - rightDir * halfWidth);
            }
        }

        // ---------------------------
        // Road mesh (3D): top + bottom + side faces
        // ---------------------------
        private void BuildRoadMesh3D(List<Vector3> center, List<Vector3> right, List<Vector3> left)
        {
            if (center == null || center.Count < 4) return;
            if (right == null || left == null) return;
            if (right.Count != center.Count || left.Count != center.Count) return;

            _roadMesh.Clear();

            int n = center.Count;
            Vector3 up = transform.up;

            // 4 verts per ring:
            // 0 Ltop, 1 Rtop, 2 Lbot, 3 Rbot
            var verts = new Vector3[n * 4];
            var uvs = new Vector2[n * 4];

            // We build:
            // Top: 2 triangles per segment
            // Bottom: 2 triangles per segment
            // Left side: 2 triangles per segment
            // Right side: 2 triangles per segment
            // => 8 triangles per segment => 24 indices per segment
            int[] tris = new int[n * 24];

            float vCoord = 0f;
            for (int i = 0; i < n; i++)
            {
                int vi = i * 4;

                Vector3 L = left[i];
                Vector3 R = right[i];

                Vector3 Lb = L - up * roadThickness;
                Vector3 Rb = R - up * roadThickness;

                verts[vi + 0] = transform.InverseTransformPoint(L);
                verts[vi + 1] = transform.InverseTransformPoint(R);
                verts[vi + 2] = transform.InverseTransformPoint(Lb);
                verts[vi + 3] = transform.InverseTransformPoint(Rb);

                if (i > 0)
                    vCoord += Vector3.Distance(center[i - 1], center[i]) / Mathf.Max(0.001f, uvTiling);

                // UVs: top and sides share same V
                uvs[vi + 0] = new Vector2(0f, vCoord);
                uvs[vi + 1] = new Vector2(1f, vCoord);
                uvs[vi + 2] = new Vector2(0f, vCoord);
                uvs[vi + 3] = new Vector2(1f, vCoord);
            }

            int ti = 0;
            for (int i = 0; i < n; i++)
            {
                int iNext = (i + 1) % n;

                int a = i * 4;
                int b = iNext * 4;

                // Top quad: Ltop->Rtop
                // a0 a1 b1 b0
                tris[ti++] = a + 0;
                tris[ti++] = b + 0;
                tris[ti++] = a + 1;
                tris[ti++] = a + 1;
                tris[ti++] = b + 0;
                tris[ti++] = b + 1;

                // Bottom quad (flip winding): Lbot->Rbot
                // a2 a3 b3 b2
                tris[ti++] = a + 3;
                tris[ti++] = b + 3;
                tris[ti++] = a + 2;
                tris[ti++] = a + 2;
                tris[ti++] = b + 3;
                tris[ti++] = b + 2;

                // Left side quad: Lbot->Ltop
                // a2 a0 b0 b2
                tris[ti++] = a + 2;
                tris[ti++] = b + 2;
                tris[ti++] = a + 0;
                tris[ti++] = a + 0;
                tris[ti++] = b + 2;
                tris[ti++] = b + 0;

                // Right side quad: Rtop->Rbot
                // a1 a3 b3 b1
                tris[ti++] = a + 1;
                tris[ti++] = b + 1;
                tris[ti++] = a + 3;
                tris[ti++] = a + 3;
                tris[ti++] = b + 1;
                tris[ti++] = b + 3;
            }

            _roadMesh.vertices = verts;
            _roadMesh.uv = uvs;
            _roadMesh.triangles = tris;

            _roadMesh.RecalculateNormals();
            _roadMesh.RecalculateBounds();
            _roadMesh.RecalculateTangents();

            _roadFilter.sharedMesh = _roadMesh;

            // Refresh collider
            _roadCollider.sharedMesh = null;
            _roadCollider.sharedMesh = _roadMesh;
        }

        // ---------------------------
        // Walls as mesh (ALWAYS outside road)
        // ---------------------------
        private void EnsureWallsObject()
        {
            if (_wallsGO != null) return;

            _wallsGO = transform.Find("_Generated_WallsMesh")?.gameObject;
            if (_wallsGO == null)
            {
                _wallsGO = new GameObject("_Generated_WallsMesh");
                _wallsGO.transform.SetParent(transform, false);
            }

            _wallsFilter = _wallsGO.GetComponent<MeshFilter>();
            if (!_wallsFilter) _wallsFilter = _wallsGO.AddComponent<MeshFilter>();

            _wallsRenderer = _wallsGO.GetComponent<MeshRenderer>();
            if (!_wallsRenderer) _wallsRenderer = _wallsGO.AddComponent<MeshRenderer>();

            _wallsCollider = _wallsGO.GetComponent<MeshCollider>();
            if (!_wallsCollider) _wallsCollider = _wallsGO.AddComponent<MeshCollider>();

            if (_wallsMesh == null)
            {
                _wallsMesh = new Mesh { name = "WallsMesh" };
                _wallsMesh.MarkDynamic();
                _wallsFilter.sharedMesh = _wallsMesh;
            }
        }

        private void DisableWallsObject()
        {
            if (_wallsGO != null) _wallsGO.SetActive(false);
        }

        private void BuildWallsMesh(List<Vector3> center, List<Vector3> right, List<Vector3> left)
        {
            EnsureWallsObject();
            _wallsGO.SetActive(true);

            if (center == null || center.Count < 4) return;
            if (right == null || left == null) return;
            if (right.Count != center.Count || left.Count != center.Count) return;

            _wallsMesh.Clear();

            Vector3 up = transform.up;
            int n = center.Count;

            // We’ll build 8 verts per ring:
            // Left wall: Lin, Lout, LoutTop, LinTop
            // Right wall: Rin, Rout, RoutTop, RinTop
            // total 8 per ring
            var verts = new List<Vector3>(n * 8);
            var uvs = new List<Vector2>(n * 8);
            var tris = new List<int>((n) * 24 * 2);

            float vAcc = 0f;

            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                int next = (i + 1) % n;

                Vector3 planarForward = Vector3.ProjectOnPlane(center[next] - center[prev], up);
                if (planarForward.sqrMagnitude < 0.0001f)
                    planarForward = Vector3.ProjectOnPlane(transform.forward, up);
                planarForward.Normalize();

                Vector3 rightDir = Vector3.Cross(up, planarForward).normalized;

                // OUTWARD is away from the road:
                Vector3 outwardRight = rightDir; // right edge goes outward in +rightDir
                Vector3 outwardLeft = -rightDir; // left edge goes outward in -rightDir

                // Make sure walls sit outside curb if curbs are enabled
                float curbOut = spawnCurbs ? (curbWidth + curbOffsetOutward) : 0f;
                float baseOut = curbOut + wallExtraOutward;

                Vector3 rightBaseCenter = right[i] + outwardRight * baseOut;
                Vector3 leftBaseCenter = left[i] + outwardLeft * baseOut;

                // Wall thickness extrudes further outward
                Vector3 rightIn = rightBaseCenter;
                Vector3 rightOut = rightBaseCenter + outwardRight * wallThickness;

                Vector3 leftIn = leftBaseCenter;
                Vector3 leftOut = leftBaseCenter + outwardLeft * wallThickness;

                Vector3 rightInTop = rightIn + up * wallHeight;
                Vector3 rightOutTop = rightOut + up * wallHeight;

                Vector3 leftInTop = leftIn + up * wallHeight;
                Vector3 leftOutTop = leftOut + up * wallHeight;

                if (i > 0)
                    vAcc += Vector3.Distance(center[i - 1], center[i]);

                // Add verts (local space)
                // Left (0..3)
                verts.Add(transform.InverseTransformPoint(leftIn));
                verts.Add(transform.InverseTransformPoint(leftOut));
                verts.Add(transform.InverseTransformPoint(leftOutTop));
                verts.Add(transform.InverseTransformPoint(leftInTop));

                // Right (4..7)
                verts.Add(transform.InverseTransformPoint(rightIn));
                verts.Add(transform.InverseTransformPoint(rightOut));
                verts.Add(transform.InverseTransformPoint(rightOutTop));
                verts.Add(transform.InverseTransformPoint(rightInTop));

                // UVs (simple)
                float v = vAcc * 0.05f;
                for (int k = 0; k < 8; k++)
                    uvs.Add(new Vector2((k % 2 == 0) ? 0f : 1f, v));
            }

            // Build quads between rings
            for (int i = 0; i < n; i++)
            {
                int iNext = (i + 1) % n;

                int a = i * 8;
                int b = iNext * 8;

                // LEFT wall: indices a+0..a+3
                // Inner face: LinTop -> Lin
                AddQuad(tris, a + 3, a + 0, b + 0, b + 3);
                // Outer face: Lout -> LoutTop
                AddQuad(tris, a + 1, a + 2, b + 2, b + 1);
                // Top face
                AddQuad(tris, a + 2, a + 3, b + 3, b + 2);
                // Bottom face
                AddQuad(tris, a + 0, a + 1, b + 1, b + 0);

                // RIGHT wall: indices a+4..a+7
                // Inner face
                AddQuad(tris, a + 4, a + 7, b + 7, b + 4);
                // Outer face
                AddQuad(tris, a + 6, a + 5, b + 5, b + 6);
                // Top face
                AddQuad(tris, a + 7, a + 6, b + 6, b + 7);
                // Bottom face
                AddQuad(tris, a + 5, a + 4, b + 4, b + 5);
            }

            _wallsMesh.SetVertices(verts);
            _wallsMesh.SetUVs(0, uvs);
            _wallsMesh.SetTriangles(tris, 0);

            _wallsMesh.RecalculateNormals();
            _wallsMesh.RecalculateBounds();
            _wallsMesh.RecalculateTangents();

            _wallsFilter.sharedMesh = _wallsMesh;

            if (wallMaterial) _wallsRenderer.sharedMaterial = wallMaterial;

            if (addWallCollider)
            {
                _wallsCollider.enabled = true;
                _wallsCollider.sharedMesh = null;
                _wallsCollider.sharedMesh = _wallsMesh;
            }
            else
            {
                _wallsCollider.enabled = false;
            }
        }

        private static void AddQuad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
            tris.Add(a);
            tris.Add(c);
            tris.Add(d);
        }

        // ---------------------------
        // Waypoints (AI)
        // ---------------------------
        private void GenerateWaypointsObjects(List<Vector3> center)
        {
            if (center == null || center.Count < 4) return;

            // parent handling
            Transform root = waypointParent != null ? waypointParent : _generatedWaypointsRoot;

            // clear
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }

            Vector3 up = transform.up;

            float acc = 0f;
            int wpIndex = 0;

            for (int i = 0; i < center.Count; i++)
            {
                int prev = (i - 1 + center.Count) % center.Count;
                int next = (i + 1) % center.Count;

                acc += Vector3.Distance(center[prev], center[i]);
                if (acc < waypointEveryMeters) continue;

                acc = 0f;

                Vector3 pos = center[i];

                Vector3 forward = Vector3.ProjectOnPlane(center[next] - center[prev], up);
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.ProjectOnPlane(transform.forward, up);

                var wp = new GameObject($"Waypoint_{wpIndex++}");
                wp.transform.position = pos + up * 0.25f;
                wp.transform.rotation = Quaternion.LookRotation(forward.normalized, up);
                wp.transform.SetParent(root, true);
            }
        }

        // ---------------------------
        // Curbs as mesh (red/white alternating) on outside of hard turns
        // Fix: do NOT connect across gaps (prevents curb "teleport bridges")
        // ---------------------------
        private void EnsureCurbMeshObject()
        {
            if (_curbsGO != null) return;

            _curbsGO = transform.Find("_Generated_CurbsMesh")?.gameObject;
            if (_curbsGO == null)
            {
                _curbsGO = new GameObject("_Generated_CurbsMesh");
                _curbsGO.transform.SetParent(transform, false);
            }

            _curbFilter = _curbsGO.GetComponent<MeshFilter>();
            if (!_curbFilter) _curbFilter = _curbsGO.AddComponent<MeshFilter>();

            _curbRenderer = _curbsGO.GetComponent<MeshRenderer>();
            if (!_curbRenderer) _curbRenderer = _curbsGO.AddComponent<MeshRenderer>();

            if (_curbMesh == null)
            {
                _curbMesh = new Mesh { name = "CurbsMesh" };
                _curbMesh.MarkDynamic();
                _curbFilter.sharedMesh = _curbMesh;
            }

            // 2 materials: red (submesh 0), white (submesh 1)
            if (curbRedMaterial != null && curbWhiteMaterial != null)
                _curbRenderer.sharedMaterials = new[] { curbRedMaterial, curbWhiteMaterial };
        }

        private void DisableCurbsObject()
        {
            if (_curbsGO != null) _curbsGO.SetActive(false);
        }

        private void BuildCurbsMesh(List<Vector3> center, List<Vector3> right, List<Vector3> left)
        {
            EnsureCurbMeshObject();
            _curbsGO.SetActive(true);

            if (center == null || center.Count < 6) return;
            if (right == null || left == null) return;
            if (right.Count != center.Count || left.Count != center.Count) return;

            _curbMesh.Clear();

            Vector3 up = transform.up;

            List<Vector3> verts = new();
            List<Vector2> uvs = new();
            List<int> trisRed = new();
            List<int> trisWhite = new();

            float acc = 0f;
            int stripe = 0;

            int lastConnectedVertexStart = -1; // start index (inner/outer pair) for previous slice
            int lastSliceIndex = -999999;

            for (int i = 0; i < center.Count; i++)
            {
                int prev = (i - 1 + center.Count) % center.Count;
                int next = (i + 1) % center.Count;

                Vector3 a = Vector3.ProjectOnPlane(center[i] - center[prev], up);
                Vector3 b = Vector3.ProjectOnPlane(center[next] - center[i], up);
                if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f) continue;

                float angle = Vector3.Angle(a, b);

                acc += Vector3.Distance(center[prev], center[i]);
                if (acc < curbEveryMeters) continue;
                acc = 0f;

                if (angle < hardTurnAngleDeg)
                {
                    // break the strip so we don't connect across gaps
                    lastConnectedVertexStart = -1;
                    lastSliceIndex = -999999;
                    continue;
                }

                Vector3 dirA = a.normalized;
                Vector3 dirB = b.normalized;
                float signed = Vector3.Dot(up, Vector3.Cross(dirA, dirB)); // + left turn, - right turn

                // outside edge
                Vector3 edgePos = (signed > 0f) ? right[i] : left[i];

                Vector3 tangent = Vector3.ProjectOnPlane(center[next] - center[prev], up);
                if (tangent.sqrMagnitude < 0.0001f) continue;
                tangent.Normalize();

                Vector3 rightDir = Vector3.Cross(up, tangent).normalized;
                Vector3 outward = (signed > 0f) ? rightDir : -rightDir;

                Vector3 basePos = edgePos + outward * curbOffsetOutward;

                Vector3 inner = basePos + up * curbHeight;
                Vector3 outer = (basePos + outward * curbWidth) + up * curbHeight;

                int vStart = verts.Count;
                verts.Add(transform.InverseTransformPoint(inner));
                verts.Add(transform.InverseTransformPoint(outer));

                uvs.Add(new Vector2(0f, stripe));
                uvs.Add(new Vector2(1f, stripe));

                // Only connect if the previous slice was "nearby" in index terms
                // (prevents long bridges across gaps)
                bool canConnect = lastConnectedVertexStart >= 0 && (i - lastSliceIndex) <= 2;

                if (canConnect)
                {
                    int v0 = lastConnectedVertexStart + 0;
                    int v1 = lastConnectedVertexStart + 1;
                    int v2 = vStart + 0;
                    int v3 = vStart + 1;

                    bool isRed = (stripe % 2 == 0);
                    var triList = isRed ? trisRed : trisWhite;

                    triList.Add(v0);
                    triList.Add(v2);
                    triList.Add(v1);
                    triList.Add(v1);
                    triList.Add(v2);
                    triList.Add(v3);
                }

                lastConnectedVertexStart = vStart;
                lastSliceIndex = i;

                stripe++;
            }

            if (verts.Count < 4)
            {
                // nothing generated
                _curbFilter.sharedMesh = _curbMesh;
                return;
            }

            _curbMesh.SetVertices(verts);
            _curbMesh.SetUVs(0, uvs);

            _curbMesh.subMeshCount = 2;
            _curbMesh.SetTriangles(trisRed, 0);
            _curbMesh.SetTriangles(trisWhite, 1);

            _curbMesh.RecalculateNormals();
            _curbMesh.RecalculateBounds();
            _curbMesh.RecalculateTangents();

            _curbFilter.sharedMesh = _curbMesh;

            if (curbRedMaterial != null && curbWhiteMaterial != null)
                _curbRenderer.sharedMaterials = new[] { curbRedMaterial, curbWhiteMaterial };
        }

        // ---------------------------
        // Cleanup
        // ---------------------------
        private void ClearPCG()
        {
            // clear waypoints
            var wpRoot = waypointParent != null ? waypointParent : transform.Find("_Generated_Waypoints");
            if (wpRoot != null) DestroyChildren(wpRoot);

            // clear meshes
            if (_roadMesh != null) _roadMesh.Clear();
            if (_wallsMesh != null) _wallsMesh.Clear();
            if (_curbMesh != null) _curbMesh.Clear();
        }

        private void DestroyChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
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
        }
    }
}