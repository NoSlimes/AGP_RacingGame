using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Splines;

namespace RacingGame._Game.Scripts.PCG
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TrackMeshExtruder : MonoBehaviour
    {
        [Header("Input")]
        public SplineContainer splineContainer;

        [Header("Sampling")]
        [Tooltip("Approx distance between generated rings along the spline (meters).")]
        [Min(0.25f)] public float stepMeters = 1.5f;

        [Header("Road")]
        [Min(1f)] public float roadWidth = 8f;
        [Min(0.01f)] public float roadThickness = 0.2f;
        public float uvTiling = 2f;
        [SerializeField] private Material roadMaterial;

        [Header("Curbs")]
        public bool generateCurbs = true;
        [Min(0.05f)] public float curbWidth = 0.35f;
        [Min(0.01f)] public float curbHeight = 0.08f;
        [SerializeField] private Material curbMaterial;

        [Header("Collider")]
        public bool addMeshCollider = true;

        [Header("Rebuild")]
        public bool rebuild;

        MeshFilter _mf;
        MeshCollider _mc;
        MeshRenderer _mr;

        void Reset()
        {
            splineContainer = GetComponent<SplineContainer>();
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
        }

        void OnEnable()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            if (addMeshCollider) _mc = GetComponent<MeshCollider>();
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();
            Build();
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
            if (!splineContainer || splineContainer.Splines.Count == 0)
                return;

            var spline = splineContainer.Splines[0];
            if (spline.Count < 2)
                return;

            float length = SplineUtility.CalculateLength(spline, splineContainer.transform.localToWorldMatrix);
            if (length <= 0.01f)
                return;

            int ringCount = Mathf.Max(2, Mathf.CeilToInt(length / stepMeters));
            bool closed = spline.Closed;
            int effectiveRings = closed ? ringCount : ringCount + 1;

            int vertsPerRing = generateCurbs ? 8 : 4;

            var verts = new List<Vector3>(effectiveRings * vertsPerRing);
            var uvs = new List<Vector2>(effectiveRings * vertsPerRing);

            // Split triangles into submeshes
            var roadTris = new List<int>(effectiveRings * 12);
            var curbTris = new List<int>(effectiveRings * 12);

            float vCoord = 0f;
            Vector3 prevPos = Vector3.zero;

            for (int i = 0; i < effectiveRings; i++)
            {
                float t;
                if (closed) t = (i / (float)ringCount) % 1f;
                else t = Mathf.Clamp01(i / (float)ringCount);

                Vector3 worldPos = SplineUtility.EvaluatePosition(spline, t);
                Vector3 worldTan = SplineUtility.EvaluateTangent(spline, t);

                worldPos = splineContainer.transform.TransformPoint(worldPos);
                worldTan = splineContainer.transform.TransformDirection(worldTan).normalized;

                Vector3 up = Vector3.up;
                Vector3 right = Vector3.Cross(up, worldTan).normalized;
                if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
                Vector3 left = -right;

                if (i == 0) prevPos = worldPos;
                float d = (i == 0) ? 0f : Vector3.Distance(prevPos, worldPos);
                vCoord += d / Mathf.Max(0.0001f, uvTiling);
                prevPos = worldPos;

                float half = roadWidth * 0.5f;

                Vector3 topL = worldPos + left * half;
                Vector3 topR = worldPos + right * half;
                Vector3 botL = topL - up * roadThickness;
                Vector3 botR = topR - up * roadThickness;

                int baseIndex = verts.Count;

                // Road verts
                verts.Add(transform.InverseTransformPoint(topL)); // 0
                verts.Add(transform.InverseTransformPoint(topR)); // 1
                verts.Add(transform.InverseTransformPoint(botL)); // 2
                verts.Add(transform.InverseTransformPoint(botR)); // 3

                uvs.Add(new Vector2(0f, vCoord));
                uvs.Add(new Vector2(1f, vCoord));
                uvs.Add(new Vector2(0f, vCoord));
                uvs.Add(new Vector2(1f, vCoord));

                if (generateCurbs)
                {
                    Vector3 curbL_inner = topL + up * curbHeight;
                    Vector3 curbL_outer = worldPos + left * (half + curbWidth) + up * curbHeight;

                    Vector3 curbR_inner = topR + up * curbHeight;
                    Vector3 curbR_outer = worldPos + right * (half + curbWidth) + up * curbHeight;

                    verts.Add(transform.InverseTransformPoint(curbL_outer)); // 4
                    verts.Add(transform.InverseTransformPoint(curbL_inner)); // 5
                    verts.Add(transform.InverseTransformPoint(curbR_inner)); // 6
                    verts.Add(transform.InverseTransformPoint(curbR_outer)); // 7

                    uvs.Add(new Vector2(-0.1f, vCoord));
                    uvs.Add(new Vector2(0.0f, vCoord));
                    uvs.Add(new Vector2(1.0f, vCoord));
                    uvs.Add(new Vector2(1.1f, vCoord));
                }

                bool isLast = (i == effectiveRings - 1);
                if (!isLast)
                {
                    AddRingConnection(roadTris, curbTris, baseIndex, baseIndex + vertsPerRing, generateCurbs);
                }
                else if (closed)
                {
                    AddRingConnection(roadTris, curbTris, baseIndex, 0, generateCurbs);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "TrackMesh";
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);

            if (generateCurbs)
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(roadTris, 0);
                mesh.SetTriangles(curbTris, 1);
            }
            else
            {
                // Only road
                mesh.subMeshCount = 1;
                mesh.SetTriangles(roadTris, 0);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _mf.sharedMesh = mesh;
            
            ApplyMaterialsToRenderer();

            if (addMeshCollider)
            {
                if (!_mc) _mc = gameObject.GetComponent<MeshCollider>();
                if (!_mc) _mc = gameObject.AddComponent<MeshCollider>();
                _mc.sharedMesh = null;
                _mc.sharedMesh = mesh;
            }
            else
            {
                if (_mc) _mc.sharedMesh = null;
            }
        }

        void ApplyMaterialsToRenderer()
        {
            if (!_mr) _mr = GetComponent<MeshRenderer>();

            if (!generateCurbs)
            {
                if (roadMaterial != null)
                    _mr.sharedMaterials = new[] { roadMaterial };
                return;
            }
            _mr.sharedMaterials = new[] { roadMaterial, curbMaterial };
        }

        static void AddRingConnection(List<int> roadTris, List<int> curbTris, int ringBaseA, int ringBaseB, bool curbs)
        {
            // Road top
            AddQuad(roadTris, ringBaseA + 0, ringBaseA + 1, ringBaseB + 0, ringBaseB + 1);
            // Road bottom
            AddQuad(roadTris, ringBaseA + 3, ringBaseA + 2, ringBaseB + 3, ringBaseB + 2);
            // Left wall
            AddQuad(roadTris, ringBaseA + 2, ringBaseA + 0, ringBaseB + 2, ringBaseB + 0);
            // Right wall
            AddQuad(roadTris, ringBaseA + 1, ringBaseA + 3, ringBaseB + 1, ringBaseB + 3);

            if (!curbs) return;

            // Curbs top strips
            AddQuad(curbTris, ringBaseA + 4, ringBaseA + 5, ringBaseB + 4, ringBaseB + 5);
            AddQuad(curbTris, ringBaseA + 6, ringBaseA + 7, ringBaseB + 6, ringBaseB + 7);

            // Inner faces (road edge -> curb inner)
            AddQuad(curbTris, ringBaseA + 0, ringBaseA + 5, ringBaseB + 0, ringBaseB + 5);
            AddQuad(curbTris, ringBaseA + 6, ringBaseA + 1, ringBaseB + 6, ringBaseB + 1);

            // Outer faces
            AddQuad(curbTris, ringBaseA + 4, ringBaseA + 0, ringBaseB + 4, ringBaseB + 0);
            AddQuad(curbTris, ringBaseA + 1, ringBaseA + 7, ringBaseB + 1, ringBaseB + 7);
        }

        static void AddQuad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }
    }
}
