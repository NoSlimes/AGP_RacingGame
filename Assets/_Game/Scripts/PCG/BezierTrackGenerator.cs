using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Splines;
using Random = UnityEngine.Random;

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

        [Header("Plane start settings")] 
        [Min(0)] public int planeStartKnotCount = 2;
        public bool usePlaneStartOnPlay = true;

        [Header("Reference")] 
        public TrackMeshExtruder meshExtruder;
        public TrackWaypointBuilder waypointBuilder;
        public CheckpointManager checkpointManager;
        public StartFinishBuilder startFinishBuilder;
        public TrackWallBuilder wallBuilder;

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
            var heightAmpModifier = PlayerPrefs.GetFloat("TrackHeightAmplitudeModifier", 1f);
            var heightNoiseModifier = PlayerPrefs.GetFloat("TrackHeightNoiseScaleModifier", 1f);
            var noiseModifier = PlayerPrefs.GetFloat("TrackNoiseModifier", 1f);
            var radiusModifier = PlayerPrefs.GetFloat("TrackRadiusModifier", 1f);
            var knotCountModifier = PlayerPrefs.GetFloat("TrackKnotCountModifier", 1f);
            var tangentStrengthModifier = PlayerPrefs.GetFloat("TrackTangentStrengthModifier", 1f);

            heightAmplitude *= heightAmpModifier;
            heightNoiseScale *= heightNoiseModifier;
            noise *= noiseModifier;
            radius *= radiusModifier;
            knotCount = Mathf.Max(4, Mathf.RoundToInt(knotCount * knotCountModifier));
            tangentStrength *= tangentStrengthModifier;

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

            // Rebuild
            if (meshExtruder)
                meshExtruder.Build();

            if (wallBuilder)
                wallBuilder.Build();
            
            if (waypointBuilder)
                waypointBuilder.Build();
            
            if (checkpointManager) 
                checkpointManager.BuildCheckpoints();
            
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
            
            // Plane start
            if (pts.Count > 0 && planeStartKnotCount > 0)
            {
                float startY = pts[0].y;
                int count = Mathf.Clamp(planeStartKnotCount, 0, knotCount);

                for (int i = 0; i < count; i++)
                {
                    var p = pts[i];
                    p.y = startY;
                    pts[i] = p;
                }

                if (usePlaneStartOnPlay && knotCount > 1)
                {
                    var last = pts[knotCount - 1];
                    last.y = startY;
                    pts[knotCount - 1] = last;
                }
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

                // Flatten tangents
                if (planeStartKnotCount > 0)
                {
                    bool inPlaneStart = i < Mathf.Clamp(planeStartKnotCount, 0, knotCount);
                    bool isLastPlane = usePlaneStartOnPlay && (i == knotCount - 1);

                    if (inPlaneStart || isLastPlane)
                    {
                        tanIn.y = 0f;
                        tanOut.y = 0f;
                    }
                }

                var knot = new BezierKnot(curr, tanIn, tanOut, Quaternion.identity);
                spline.Add(knot);
            }

            splineContainer.Spline.Clear();
            splineContainer.Spline.Add(spline);
        }
    }
}