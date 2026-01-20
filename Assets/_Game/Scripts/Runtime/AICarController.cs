using NUnit.Framework;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Cinemachine;

public struct PathPoint
{
    public Vector3 position;
    public Vector3 forward;
    public float curvature;
    public float trackWidth;
}

namespace RacingGame
{
    [Serializable]
    public class AICarController : ICarInputs, ITickable
    {
        public AICarController()
        {}

        public AICarController(List<Vector3> center, List<Vector3> right, List<Vector3> left)
        { 
            CenterLine = center;
            RightEdge = right;
            LeftEdge = left;
        }

        public Vector2 MoveInput { get; private set; }
        public bool HandBrakeInput { get; private set; }
        public bool NitroInput { get; private set; }

        //private PlayerCarInputs playerCar;
        private Vector3 targetWP;

        [SerializeField]
        private PCGManager pCG;

        public List<PathPoint> pathPoints;
        private float currentDistance;
        private float totalDistance;
        private int currentindex = 0;
        private float maxSpeed = 50;
        private float currentSpeed = 20;
        private float maxLatAccel = 8.0f;
        private float steerInput;
        private float signedAngle;
        private float currentOffset = 0;
        private Vector3 targetPosition;
        private float throttle = 0;
        private bool brake;
        private bool brakingHard;
        private bool sharpTurnAhead = false;
        private float sharpTurnCurvature = 0.08f;
        private Transform thisTransform;
        private Rigidbody rigidBody;
        List<Vector3> CenterLine;
        List<Vector3> RightEdge;
        List<Vector3> LeftEdge;
        int n;
        float[] SegmentLength;
        float[] CumulativeLength;

        float bestDistSq = float.MaxValue;
        int bestSegment = 0;
        float bestT = 0f;
        Vector3 bestClosest = Vector3.zero;

        public void Initialize(Transform transform)
        { 
            thisTransform = transform;

            rigidBody = thisTransform.GetComponent<Rigidbody>();
        }

        public void PostInitialize()
        {
            if (pCG)
            {
                CenterLine = pCG.Centerline;
                RightEdge = pCG.RightEdge;
                LeftEdge = pCG.LeftEdge;
            }

            n = CenterLine.Count;

            pathPoints = new List<PathPoint>(n);

            for (int i = 1; i < n; i++)
            {
                float angle;
                float distance;

                if ((i + 1) < n)
                {
                    Vector3 a = (CenterLine[i] - CenterLine[i - 1]).normalized;
                    Vector3 b = (CenterLine[i + 1] - CenterLine[i]).normalized;
                    angle = Vector3.Angle(a, b) * Mathf.Deg2Rad;
                    distance = Vector3.Distance(CenterLine[i - 1], CenterLine[i + 1]);
                }
                else
                {
                    Vector3 a = (CenterLine[i] - CenterLine[i - 1]).normalized;
                    Vector3 b = (CenterLine[0] - CenterLine[i]).normalized;
                    angle = Vector3.Angle(a, b) * Mathf.Deg2Rad;
                    distance = Vector3.Distance(CenterLine[i - 1], CenterLine[0]);
                }

                pathPoints.Add(new PathPoint
                {
                    position = CenterLine[i - 1],
                    forward = (CenterLine[i] - CenterLine[i - 1]).normalized,
                    curvature = angle / distance,
                    trackWidth = Vector3.Distance(LeftEdge[i - 1], RightEdge[i - 1])

                });
            }

            SegmentLength = new float[n - 1];
            CumulativeLength = new float[n];

            CumulativeLength[0] = 0f;

            for (int i = 0; i < n - 1; i++)
            {
                SegmentLength[i] = Vector3.Distance(CenterLine[i], CenterLine[i + 1]);
                CumulativeLength[i + 1] = CumulativeLength[i] + SegmentLength[i];
            }

            totalDistance = CumulativeLength[n - 1];

            currentSpeed = rigidBody.linearVelocity.magnitude;

            GameManager.Instance.RegisterTickable(this);
        }

        public void Deinitialize()
        { }

        public void Tick()
        {
            FindClosestSegment();
            currentDistance = CumulativeLength[bestSegment] + bestT * SegmentLength[bestSegment];

            float progress = currentDistance / totalDistance;

            GetSteering();
            Offset();
            ThrottleOrBrake();

            MoveInput = new Vector2(steerInput, throttle);
            HandBrakeInput = brake;
            NitroInput = ShouldBoost();

            //Gizmos.color = Color.red;
            //Gizmos.DrawSphere(bestClosest, 0.3f);
            //Gizmos.color = Color.green;
            //Gizmos.DrawSphere(targetPosition, 0.3f);
        }

        bool ShouldBoost()
        {
            return false;
        }

        public void GetSteering()
        {
            float lookAheadDistance = Mathf.Lerp(15f, 40f, currentSpeed / maxSpeed);
            if(sharpTurnAhead)
            {
                lookAheadDistance *= 0.6f;
            }
            float targetS = currentDistance + lookAheadDistance;
            targetS %= totalDistance;

            targetPosition = PositionAtDistance(targetS);

            //Any different from pathForward?
            Vector3 pathForwardv2 = (PositionAtDistance(targetS + 1f) - PositionAtDistance(targetS)).normalized;

            Vector3 targetDirection = (targetPosition - thisTransform.position).normalized;

            signedAngle = Vector3.SignedAngle(thisTransform.forward, targetDirection, Vector3.up);

            float maxSteer = 30f;

            steerInput = Mathf.Clamp(signedAngle / maxSteer, -1f, 1f);

            if(brakingHard)
            {
                steerInput *= 0.6f;
            }
        }

        public void ThrottleOrBrake()
        {
            float averageCurvature = EstimateCurvature();
            sharpTurnAhead = averageCurvature > sharpTurnCurvature;
            float safeSpeed = Mathf.Sqrt(maxLatAccel / Mathf.Max(averageCurvature, 0.001f));

            float brakingDistance = EstimateBrakingDistance(currentSpeed, safeSpeed);
            float distanceToTurn = 30f;

            if (sharpTurnAhead)
            {
                safeSpeed *= 0.75f;
                distanceToTurn = Mathf.Lerp(20f, 60f, averageCurvature / 0.08f);
            }

            brakingHard = sharpTurnAhead && currentSpeed > safeSpeed * 1.1f;

            float widthFactor = WidthFactor();
            float widthSpeedMultiplier = Mathf.Lerp(0.9f, 1.05f, widthFactor);
            safeSpeed *= widthSpeedMultiplier;

            float speedError = safeSpeed - currentSpeed;
            float desiredThrottle;

            //make public
            float throttleGain = 0.5f;

            if (speedError > 0)
            {
                desiredThrottle = Mathf.Clamp01(speedError * throttleGain);
                brake = false;
            }
            else
            {
                desiredThrottle = 0;
                brake = true;
            }

            if(brakingDistance > distanceToTurn)
            {
                desiredThrottle = 0;
                brake = true;
            }

            //make public
            float throttleResponse = 5.0f;
            throttle = Mathf.MoveTowards(throttle, desiredThrottle, throttleResponse * Time.deltaTime);
        }

        public void Offset()
        {
            Vector3 pathforward = pathPoints[bestSegment].forward;
            Vector3 pathRight = Vector3.Cross(Vector3.up, pathforward);
            Vector3 toCar = thisTransform.position - bestClosest;
            float lateralOffset = Vector3.Dot(toCar, pathRight);

            //what is recoveryDistance?
            Vector3 recoveryTarget = bestClosest + pathforward * 10;

            float halfWidth = pathPoints[bestSegment].trackWidth / 2;
            float margin = 0.5f;

            float sharpTurnStrength = sharpTurnAhead ? 1.0f : 0.7f;
            float turnSign = GetPathTurnSign(bestSegment);
            float desiredOffset = -turnSign * halfWidth * sharpTurnStrength;

            desiredOffset = Mathf.Clamp(desiredOffset, -halfWidth + margin, halfWidth - margin);

            if (Mathf.Abs(lateralOffset) > halfWidth)
            {
                desiredOffset = 0f;
            }

            //make public
            float offsetResponse = 2.5f;
            currentOffset = Mathf.MoveTowards(currentOffset, desiredOffset, offsetResponse * Time.deltaTime);

            targetPosition += pathRight * currentOffset;
        }

        public float WidthFactor()
        {
            float widthAhead = 0f;
            int widthSamples = 3;
            int segIdx = 0;

            for(int i = 0; i < widthSamples; i++)
            {
                if((bestSegment + i) >= n - 1)
                {
                    segIdx = (bestSegment + i) % (n - 1);
                }
                else
                {
                    segIdx = bestSegment + i;
                }
                
                int idx = Mathf.Min(segIdx, n - 1);
                widthAhead += pathPoints[idx].trackWidth;
            }

            widthAhead /= widthSamples;

            //make public
            float minWidth = 6f;
            float maxWidth = 14f;

            float widthFactor = Mathf.InverseLerp(minWidth, maxWidth, widthAhead);

            return widthFactor;
        }

        public void FindClosestSegment()
        {
            bestDistSq = float.MaxValue;
            
            for(int i = 0; i < n - 1; i++)
            {
                Vector3 a = CenterLine[i];
                Vector3 b = CenterLine[i + 1];

                Vector3 ab = b - a;
                float abLenSq = ab.sqrMagnitude;
                if (abLenSq < 0.0001f)
                    continue;
                Vector3 ac = thisTransform.position - a;

                float t = Vector3.Dot(ac, ab) / ab.sqrMagnitude;
                t = Mathf.Clamp01(t);

                Vector3 closest = a + t * ab;

                float distSq = (thisTransform.position - closest).sqrMagnitude;

                if(distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestSegment = i;
                    bestT = t; 
                    bestClosest = closest;
                }
            }
        }

        public Vector3 PositionAtDistance(float s)
        {
            for (int i = 0; i < SegmentLength.Length; i++)
            { 
                if(s <= CumulativeLength[i + 1])
                {
                    float t = (s - CumulativeLength[i]) / SegmentLength[i];
                    return Vector3.Lerp(CenterLine[i], CenterLine[i + 1], t);
                }
            
            }
            return CenterLine[n - 1];
        }

        public float EstimateCurvature()
        {
            float lookAhead = sharpTurnAhead ? 80f : 50f;
            int samples = 5;
            float curvatureSum = 0.0f;

            for (int i = 1; i <= samples; i++)
            {
                float s0 = currentDistance + (i - 1) * (lookAhead / samples);
                float s1 = currentDistance + i * (lookAhead / samples);

                Vector3 p0 = PositionAtDistance(s0);
                Vector3 p1 = PositionAtDistance(s1);
                Vector3 p2 = PositionAtDistance(s1 + 0.5f);

                Vector3 d0 = (p1 - p0).normalized;
                Vector3 d1 = (p2 - p1).normalized;

                float angle = Vector3.Angle(d0, d1) * Mathf.Deg2Rad;
                float segmentLength = (p2 - p0).magnitude;

                float curvateture = angle / Mathf.Max(segmentLength, 0.001f);
                curvatureSum += curvateture;
            }

            float averageCurvature = curvatureSum / samples;
            return averageCurvature;
        }

        float GetPathTurnSign(int seg)
        {
            int i1 = (seg + 1) % (n - 1);
            int i2 = (seg + 2) % (n - 1);

            Vector3 d1 = pathPoints[i1].forward;
            Vector3 d2 = pathPoints[i2].forward;

            float sign = Mathf.Sign(Vector3.Cross(d1, d2).y);
            return sign;
        }

        float EstimateBrakingDistance(float speed, float targetspeed)
        {
            float decel = maxLatAccel * 1.2f;
            return (speed * speed - targetspeed * targetspeed) / (2f * decel);
        }
    }
}
