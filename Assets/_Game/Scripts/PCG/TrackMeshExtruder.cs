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
        
        [Header("Grass")]
        public bool generateGrass = true;
        [Min(0.01f)] public float grassWidth = 4f;
        public float grassYOffset = 0.01f;
        [SerializeField] private Material grassMaterial;
        public PhysicsMaterial grassPhysicsMaterial;

        [Header("Collider")]
        public bool addMeshCollider = true;

        [Header("Rebuild")]
        public bool rebuild;

        MeshFilter _mf;
        MeshCollider _mc;
        MeshCollider _mc_Grass;
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
            
            if (addMeshCollider)
            {
                var colliders = GetComponents<MeshCollider>();

                if (colliders.Length == 0)
                {
                    _mc = gameObject.AddComponent<MeshCollider>();
                    _mc_Grass = gameObject.AddComponent<MeshCollider>();
                }
                else if (colliders.Length == 1)
                {
                    _mc = colliders[0];
                    _mc_Grass = gameObject.AddComponent<MeshCollider>();
                }
                else if (colliders.Length >= 2)
                {
                    _mc = colliders[0];
                    _mc_Grass = colliders[1];
                }
            }
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
            // Make Build safe even if another script calls it before OnEnable
            if (!_mf) _mf = GetComponent<MeshFilter>();
            if (!_mr) _mr = GetComponent<MeshRenderer>();
            if (addMeshCollider && !_mc) _mc = GetComponent<MeshCollider>();
            if (addMeshCollider && !_mc_Grass) _mc_Grass = GetComponent<MeshCollider>();
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();

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

            int vertsPerRing = 4;
            if (generateCurbs) vertsPerRing += 4;
            if (generateGrass) vertsPerRing += 4;

            var verts = new List<Vector3>(effectiveRings * vertsPerRing);
            var uvs = new List<Vector2>(effectiveRings * vertsPerRing);

            // Split triangles into submeshes
            var roadTris = new List<int>(effectiveRings * 12);
            var curbTris = new List<int>(effectiveRings * 12);
            var grassTris = new List<int>(effectiveRings * 12);

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

                if (generateGrass)
                {
                    float sideStart = half + (generateCurbs ? curbWidth : 0f);
                    
                    // Road height
                    Vector3 grassL_inner = worldPos + left * sideStart + up * grassYOffset;
                    Vector3 grassL_outer = worldPos + left * (sideStart + grassWidth) + up * grassYOffset;

                    Vector3 grassR_inner = worldPos + right * sideStart + up * grassYOffset;
                    Vector3 grassR_outer = worldPos + right * (sideStart + grassWidth) + up * grassYOffset;

                    // L_inner, L_outer, R_inner, R_outer
                    verts.Add(transform.InverseTransformPoint(grassL_inner)); // 0
                    verts.Add(transform.InverseTransformPoint(grassL_outer)); // 1
                    verts.Add(transform.InverseTransformPoint(grassR_inner)); // 2
                    verts.Add(transform.InverseTransformPoint(grassR_outer)); // 3

                    // UVs
                    uvs.Add(new Vector2(-0.2f, vCoord));
                    uvs.Add(new Vector2(-0.7f, vCoord));
                    uvs.Add(new Vector2(1.2f, vCoord));
                    uvs.Add(new Vector2(1.7f, vCoord));
                }
                
                bool isLast = (i == effectiveRings - 1);
                if (!isLast)
                {
                    AddRingConnection(roadTris, curbTris, grassTris, baseIndex, baseIndex + vertsPerRing, generateCurbs, generateGrass);
                }
                else if (closed)
                {
                    AddRingConnection(roadTris, curbTris, grassTris, baseIndex, 0, generateCurbs, generateGrass);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "TrackMesh";
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);

            // Only road
            int subMeshCount = 1 + (generateCurbs ? 1 : 0) + (generateGrass ? 1 : 0);
            mesh.subMeshCount = subMeshCount;

            // Curbs + Grass
            int sm = 0;
            mesh.SetTriangles(roadTris, sm++);
            if (generateCurbs) mesh.SetTriangles(curbTris, sm++);
            if (generateGrass) mesh.SetTriangles(grassTris, sm++);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _mf.sharedMesh = mesh;

            if (generateGrass && _mc_Grass != null)
            {
                // Create collision mesh
                var grassMesh = new Mesh();
                grassMesh.name = "GrassColliderMesh";
                grassMesh.SetVertices(verts);

                // Grass tri indices into verts
                grassMesh.SetTriangles(grassTris, 0);

                grassMesh.RecalculateNormals();
                grassMesh.RecalculateBounds();

                _mc_Grass.sharedMesh = null;
                _mc_Grass.sharedMesh = grassMesh;

                _mc_Grass.material = grassPhysicsMaterial;
            }
            else
            {
                if (_mc_Grass != null) _mc_Grass.sharedMesh = null;
            }
            
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
            _mr.sharedMaterials = new[] { roadMaterial, curbMaterial, grassMaterial };
        }

        static void AddRingConnection(List<int> roadTris, List<int> curbTris, List<int> grassTris, int ringBaseA, int ringBaseB, bool curbs, bool grass)
        {
            // Road top
            AddQuad(roadTris, ringBaseA + 0, ringBaseA + 1, ringBaseB + 0, ringBaseB + 1);
            // Road bottom
            AddQuad(roadTris, ringBaseA + 3, ringBaseA + 2, ringBaseB + 3, ringBaseB + 2);
            // Left wall
            AddQuad(roadTris, ringBaseA + 2, ringBaseA + 0, ringBaseB + 2, ringBaseB + 0);
            // Right wall
            AddQuad(roadTris, ringBaseA + 1, ringBaseA + 3, ringBaseB + 1, ringBaseB + 3);
            
            // Offsets
            int curbOffset = 4;
            int grassOffset = 4 + (curbs ? 4 : 0);

            if (curbs)
            {
                int outerL_A = ringBaseA + curbOffset + 0;
                int innerL_A = ringBaseA + curbOffset + 1;
                int innerR_A = ringBaseA + curbOffset + 2;
                int outerR_A = ringBaseA + curbOffset + 3;

                int outerL_B = ringBaseB + curbOffset + 0;
                int innerL_B = ringBaseB + curbOffset + 1;
                int innerR_B = ringBaseB + curbOffset + 2;
                int outerR_B = ringBaseB + curbOffset + 3;

                // tops
                AddQuad(curbTris, outerL_A, innerL_A, outerL_B, innerL_B);
                AddQuad(curbTris, innerR_A, outerR_A, innerR_B, outerR_B);

                // inner faces (road edge -> curb inner)
                AddQuad(curbTris, ringBaseA + 0, innerL_A, ringBaseB + 0, innerL_B);
                AddQuad(curbTris, innerR_A, ringBaseA + 1, innerR_B, ringBaseB + 1);

                // outer faces
                AddQuad(curbTris, outerL_A, ringBaseA + 0, outerL_B, ringBaseB + 0);
                AddQuad(curbTris, ringBaseA + 1, outerR_A, ringBaseB + 1, outerR_B);
            }
            
            if (grass)
            {
                int gLIn_A = ringBaseA + grassOffset + 0;
                int gLOut_A = ringBaseA + grassOffset + 1;
                int gRIn_A = ringBaseA + grassOffset + 2;
                int gROut_A = ringBaseA + grassOffset + 3;

                int gLIn_B = ringBaseB + grassOffset + 0;
                int gLOut_B = ringBaseB + grassOffset + 1;
                int gRIn_B = ringBaseB + grassOffset + 2;
                int gROut_B = ringBaseB + grassOffset + 3;

                // grass top strips
                AddQuad(grassTris, gLOut_A, gLIn_A, gLOut_B, gLIn_B); // left
                AddQuad(grassTris, gRIn_A, gROut_A, gRIn_B, gROut_B); // right
            }
        }

        static void AddQuad(List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }
    }
}
