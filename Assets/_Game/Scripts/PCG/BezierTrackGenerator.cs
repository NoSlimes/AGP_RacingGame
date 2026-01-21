using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Splines;

namespace RacingGame._Game.Scripts.PCG
{
    [ExecuteAlways]
    public class BezierTrackGenerator : MonoBehaviour
    {
        [Header("Spline")] public SplineContainer splineContainer;

        [Header("PCG Shape")] [Min(4)] public int knotCount = 10;
        public float radius = 60f;
        public float noise = 20f;
        public float tangentStrength = 0.35f;

        [Header("Height (Hills)")] public float heightAmplitude = 12f;
        public float heightNoiseScale = 0.08f;

        [Header("Seed")] public int seed = 67;
        public bool regenerate;
        public bool randomizeSeedOnPlay = true;
        public bool useSavedSeed = false;
        [Tooltip("PlayerPrefs key used when useSavedSeed is enabled.")]
        public string savedSeedKey = "PCG_TRACK_SEED";
        [Tooltip("If > 0, forces this seed at runtime (overrides random).")]
        public int debugSeedOverride = 0;

        [Header("Auto Build Dependencies")]
        public TrackMeshExtruder meshExtruder;
        public TrackWaypointBuilder waypointBuilder;

        [Header("Reference")] 
        public StartFinishBuilder startFinishBuilder;

        private void Reset()
        {
            if (!splineContainer) splineContainer = GetComponent<SplineContainer>();
            if (!splineContainer) splineContainer = gameObject.AddComponent<SplineContainer>();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                GenerateOnPlayIfWanted();
            }
        }

        private void Update()
        {
            if (regenerate)
            {
                regenerate = false;
                GenerateAndRebuildAll();
            }
        }

        private void GenerateOnPlayIfWanted()
        {
            if (!randomizeSeedOnPlay && debugSeedOverride <= 0 && !useSavedSeed)
            {
                // Use whatever seed is already set
                GenerateAndRebuildAll();
                return;
            }

            int newSeed;

            if (debugSeedOverride > 0)
            {
                newSeed = debugSeedOverride;
            }
            else if (useSavedSeed)
            {
                if (!PlayerPrefs.HasKey(savedSeedKey))
                {
                    PlayerPrefs.SetInt(savedSeedKey, Random.Range(1, int.MaxValue));
                    PlayerPrefs.Save();
                }

                newSeed = PlayerPrefs.GetInt(savedSeedKey);
            }
            else
            {
                // Random every play
                newSeed = Random.Range(1, int.MaxValue);
            }

            seed = newSeed;
            GenerateAndRebuildAll();
            Debug.Log($"[BezierTrackGenerator] Generated track with seed: {seed}");
        }

        public void GenerateAndRebuildAll()
        {
            Generate();

            // Rebuild road + waypoints + goalline
            if (meshExtruder)
                meshExtruder.Build();

            if (waypointBuilder)
                waypointBuilder.Build();

            if (startFinishBuilder)
                startFinishBuilder.TryBuild();
        }

        public void Generate()
        {
            if (!splineContainer) return;

            Random.InitState(seed);

            var spline = new Spline();
            spline.Closed = true;

            // Create rough points in a circle
            List<Vector3> pts = new List<Vector3>(knotCount);

            for (int i = 0; i < knotCount; i++)
            {
                float t = i / (float)knotCount;
                float ang = t * Mathf.PI * 2f;

                float r = radius + Random.Range(-noise, noise);
                float x = Mathf.Cos(ang) * r;
                float z = Mathf.Sin(ang) * r;

                // height via Perlin
                float h = (Mathf.PerlinNoise((x + 999f) * heightNoiseScale, (z + 999f) * heightNoiseScale) - 0.5f) * 2f;
                float y = h * heightAmplitude;

                pts.Add(new Vector3(x, y, z));
            }

            // Build knots for cubic Bezier smoothness
            for (int i = 0; i < knotCount; i++)
            {
                Vector3 prev = pts[(i - 1 + knotCount) % knotCount];
                Vector3 curr = pts[i];
                Vector3 next = pts[(i + 1) % knotCount];
                
                Vector3 dir = (next - prev).normalized;

                // scale tangents
                float localLen = Vector3.Distance(curr, next);
                float handleLen = localLen * tangentStrength;

                // tangents in/out
                Vector3 tanIn = -dir * handleLen;
                Vector3 tanOut = dir * handleLen;

                var knot = new BezierKnot(curr, tanIn, tanOut, Quaternion.identity);
                spline.Add(knot);
            }

            splineContainer.Spline.Clear();
            splineContainer.Spline.Add(spline);
        }
    }
}