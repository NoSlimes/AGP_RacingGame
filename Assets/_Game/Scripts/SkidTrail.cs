using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SkidTrail : MonoBehaviour
    {
        [SerializeField] private float width = 0.35f;
        [SerializeField] private float minDistance = 0.1f;
        [SerializeField] private int maxSegments = 256;

        private Mesh mesh;
        private readonly List<Vector3> vertices = new();
        private readonly List<int> triangles = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<Color> colors = new();

        private Vector3 lastPos;
        private bool hasLast;

        private void Awake()
        {
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            mesh = new Mesh();
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().mesh = mesh;
        }

        public void AddPoint(Vector3 pos, Vector3 normal, float intensity)
        {
            if (hasLast && Vector3.Distance(lastPos, pos) < minDistance) return;

            Vector3 forward = hasLast ? (pos - lastPos).normalized : transform.forward;
            Vector3 right = 0.5f * width * Vector3.Cross(normal, forward).normalized;

            int index = vertices.Count;

            vertices.Add(pos + right);
            vertices.Add(pos - right);

            float v = hasLast ? uvs[^2].y + Vector3.Distance(lastPos, pos) : 0f;
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));

            Color c = new(1f, 1f, 1f, intensity);
            colors.Add(c);
            colors.Add(c);

            if (hasLast)
            {
                triangles.Add(index - 2);
                triangles.Add(index - 1);
                triangles.Add(index);
                triangles.Add(index + 1);
                triangles.Add(index);
                triangles.Add(index - 1);
                if (vertices.Count / 2 > maxSegments) TrimOldestSegment();
            }

            lastPos = pos;
            hasLast = true;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();
        }

        public void EndTrail()
        {
            hasLast = false;
        }

        private void TrimOldestSegment()
        {
            vertices.RemoveRange(0, 2);
            uvs.RemoveRange(0, 2);
            colors.RemoveRange(0, 2);
            triangles.RemoveRange(0, 6);
            for (int i = 0; i < triangles.Count; i++) triangles[i] -= 2;
        }

    }
}
