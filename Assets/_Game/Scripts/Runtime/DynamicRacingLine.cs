using RacingGame._Game.Scripts.PCG;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class DynamicRacingLine : MonoBehaviour
    {
        private TrackWaypointBuilder waypointBuilder;
        private Car playerCar;

        [Header("Visual Settings")]
        [SerializeField] private float ribbonWidth = 0.5f;
        [SerializeField] private float heightOffset = 0.1f;
        [SerializeField] private int visibleAheadCount = 40;
        [SerializeField] private int visibleBehindCount = 5;
        [SerializeField] private float uvTiling = 1.0f; 

        [Header("Visibility Logic")]
        [SerializeField] private bool forceAlwaysVisible = false;
        [SerializeField] private float speedDropThreshold = 3f;

        [Header("Speed Sensitivity")]
        [SerializeField] private float redZoneMargin = 5f;

        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private List<Vector3> vertices = new();
        private List<Color> colors = new();
        private List<Vector2> uvs = new();
        private List<int> triangles = new();

        private void OnEnable() => GameManager.Instance.OnPlayerCarAssigned += HandlePlayerCarAssigned;
        private void OnDisable() => GameManager.Instance.OnPlayerCarAssigned -= HandlePlayerCarAssigned;
        private void HandlePlayerCarAssigned(Car car) => playerCar = car;

        private void Awake()
        {
            waypointBuilder = FindFirstObjectByType<TrackWaypointBuilder>();
            meshRenderer = GetComponent<MeshRenderer>();
            mesh = new Mesh { name = "DynamicRacingLineMesh" };
            GetComponent<MeshFilter>().mesh = mesh;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (playerCar == null || waypointBuilder == null || waypointBuilder.Centerline.Count < 2)
            {
                meshRenderer.enabled = false;
                return;
            }

            float currentSpeed = playerCar.Rigidbody.linearVelocity.magnitude;
            int closestIdx = GetClosestWaypointIndex(playerCar.Rigidbody.position);

            bool showLine = forceAlwaysVisible || CheckIfLineRequired(closestIdx);
            meshRenderer.enabled = showLine;

            if (showLine) UpdateMesh(closestIdx, currentSpeed);
        }

        private bool CheckIfLineRequired(int centerIdx)
        {
            int total = waypointBuilder.Centerline.Count;
            for (int i = 0; i < visibleAheadCount; i++)
            {
                int idx = (centerIdx + i) % total;
                if (waypointBuilder.Zones[idx] == WaypointZoneType.Brake ||
                    waypointBuilder.RecommendedSpeeds[idx] < (waypointBuilder.maxRecommendedSpeed - speedDropThreshold))
                    return true;
            }
            return false;
        }

        private void UpdateMesh(int centerIdx, float currentSpeed)
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            uvs.Clear();

            int total = waypointBuilder.Centerline.Count;
            int start = centerIdx - visibleBehindCount;
            int end = centerIdx + visibleAheadCount;
            int segmentCount = end - start;

            for (int i = 0; i <= segmentCount; i++)
            {
                int dataIdx = (start + i + total) % total;

                Vector3 pos = waypointBuilder.Centerline[dataIdx];
                Vector3 nextPos = waypointBuilder.Centerline[(dataIdx + 1) % total];
                Vector3 forward = (nextPos - pos).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                vertices.Add(pos + (right * ribbonWidth * 0.5f) + (Vector3.up * heightOffset));
                vertices.Add(pos - (right * ribbonWidth * 0.5f) + (Vector3.up * heightOffset));

                // UV Logic: X is along the track (tiling), Y is across the ribbon (0 to 1)
                uvs.Add(new Vector2(i * uvTiling, 1));
                uvs.Add(new Vector2(i * uvTiling, 0));

                float targetSpeed = waypointBuilder.RecommendedSpeeds[dataIdx];
                Color pColor = Color.green;

                if (currentSpeed > targetSpeed + redZoneMargin) pColor = Color.red;
                else if (currentSpeed > targetSpeed) pColor = Color.Lerp(Color.yellow, Color.red, (currentSpeed - targetSpeed) / redZoneMargin);

                float alpha = 1f;
                if (i > segmentCount - 10) alpha = (segmentCount - i) / 10f;
                if (i < 5) alpha = i / 5f;
                pColor.a = alpha;

                colors.Add(pColor);
                colors.Add(pColor);

                if (i < segmentCount)
                {
                    int b = i * 2;
                    triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
                    triangles.Add(b + 1); triangles.Add(b + 3); triangles.Add(b + 2);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
        }

        int GetClosestWaypointIndex(Vector3 playerPos)
        {
            float minDist = float.MaxValue;
            int index = 0;
            for (int i = 0; i < waypointBuilder.WaypointPositions.Count; i++)
            {
                float dist = Vector3.SqrMagnitude(playerPos - waypointBuilder.WaypointPositions[i]);
                if (dist < minDist) { minDist = dist; index = i; }
            }
            return index;
        }
    }
}