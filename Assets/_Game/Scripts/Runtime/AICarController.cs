using NUnit.Framework;
using System.Collections.Generic;
using System;

using UnityEngine;
using UnityEngine.UIElements;
using Unity.Cinemachine;
using System.Linq;
using UnityEditor;

public struct PathPoint
{
    public Vector3 position;
    public Vector3 forward;
    public float curvature;
    public float trackWidth;
}

public struct CarContexts
{
    public float Distance;
    public float ForwardDot;
    public float LateralDot;
    public float Speed;
}

namespace RacingGame
{
    [Serializable]
    public class AICarController : ICarInputs, ITickable
    {
        public AICarController()
        { }

        public AICarController(List<Vector3> center, List<Vector3> right, List<Vector3> left)
        {
            CenterLine = center;
            RightEdge = right;
            LeftEdge = left;
        }

        public Vector2 MoveInput { get; private set; }
        public bool HandBrakeInput { get; private set; }
        public bool NitroInput { get; private set; }

        CarContexts carAhead;
        CarContexts carRight;
        CarContexts carLeft;
        CarContexts carBack;

        public List<PathPoint> pathPoints;
        IReadOnlyList<Car> AllCars;
        private List<CarContexts> nearbyCarContext = new List<CarContexts>();

        private float currentDistance;
        private float totalDistance;
        private int currentindex = 0;
        private float maxSpeed = 50;
        private float currentSpeed = 0;
        private float lateralSpeed;
        private float maxLatAccel = 8.0f;
        private float steerInput;
        private float signedAngle;
        private float currentOffset = 0;
        private Vector3 targetPosition;
        float distanceToTurn;
        float widthFactor;
        float averageCurvature;
        float curveFactor;
        private float throttle = 0;
        private bool brake;
        private bool brakingHard;
        private bool sharpTurnAhead = false;
        float StuckTime;
        bool canOvertake;
        bool boxedIn;
        bool ShouldBoost;
        bool Stuck;
        float overTakeSide;
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

            AllCars = GameManager.Instance.AllCars;

            carAhead = default;
            carRight = default;
            carLeft = default;
            carBack = default;

            carAhead.Distance = float.MaxValue;
            carRight.Distance = float.MaxValue;
            carLeft.Distance = float.MaxValue;
            carBack.Distance = float.MaxValue;

            GameManager.Instance.RegisterTickable(this);
        }

        public void Deinitialize()
        { }

        public void Tick()
        {
            nearbyCarContext.Clear();

            foreach (Car car in AllCars)
            {
                if (car.transform == thisTransform)
                {
                    continue;
                }

                Vector3 toOther = car.transform.position - thisTransform.position;
                float distance = toOther.magnitude;

                Vector3 toOtherNorm = toOther.normalized;
                float forwardDot = Vector3.Dot(thisTransform.forward, toOtherNorm);
                float lateralDot = Vector3.Dot(thisTransform.right, toOtherNorm);

                float speed = car.Rigidbody.linearVelocity.magnitude;

                CarContexts ctx = new CarContexts
                {
                    Distance = distance,
                    ForwardDot = forwardDot,
                    LateralDot = lateralDot,
                    Speed = speed
                };

                nearbyCarContext.Add(ctx);
            }

            //float closestAhead = float.MaxValue;
            //float closestRight = float.MaxValue;
            //float closestLeft = float.MaxValue;
            //float closestBack = float.MaxValue;

            foreach (CarContexts car in nearbyCarContext)
            {
                if (car.ForwardDot > 0.3f)
                {
                    if (car.Distance < carAhead.Distance)
                    {
                        carAhead = car;
                        //closestAhead = carAhead.Distance;
                    }
                }
                if (car.LateralDot > 0.5f)
                {
                    if (car.Distance < carRight.Distance)
                    {
                        carRight = car;
                        //closestRight = carRight.Distance;
                    }
                }
                if (car.LateralDot < -0.5f)
                {
                    if (car.Distance < carLeft.Distance)
                    {
                        carLeft = car;
                        //closestLeft = carLeft.Distance;
                    }
                }
                if (car.ForwardDot < -0.5f)
                {
                    if (car.Distance < carBack.Distance)
                    {
                        carBack = car;
                        //closestBack = carBack.Distance;
                    }
                }
            }

            boxedIn = carAhead.Distance < 10f && carRight.Distance < 5f && carLeft.Distance < 5f;

            currentSpeed = rigidBody.linearVelocity.magnitude;
            Vector3 localVelocity = thisTransform.InverseTransformDirection(rigidBody.linearVelocity);
            lateralSpeed = localVelocity.x;

            FindClosestSegment();
            currentDistance = CumulativeLength[bestSegment] + bestT * SegmentLength[bestSegment];

            float progress = currentDistance / totalDistance;

            averageCurvature = EstimateCurvature();
            sharpTurnAhead = averageCurvature > sharpTurnCurvature;
            curveFactor = Mathf.InverseLerp(0.02f, 0.08f, averageCurvature);
            widthFactor = WidthFactor();
            canOvertake = widthFactor > 0.6f && !sharpTurnAhead && carAhead.ForwardDot > 0.6f && carAhead.Distance < 15f;
            GetSteering();
            Offset();
            ThrottleOrBrake();
            UpdateStuck();

            if (Stuck)
            {
                throttle = -0.6f;
                brake = false;
                steerInput = -Mathf.Sign(steerInput);
            }

            MoveInput = new Vector2(steerInput, throttle);
            HandBrakeInput = brake;
            NitroInput = ShouldBoost;
        }

        void UpdateStuck()
        {
            bool tryingToMove = Mathf.Abs(throttle) > 0.3f && Mathf.Abs(steerInput) > 0.01f;

            bool notMoving = currentSpeed < 1.0f;

            if (tryingToMove && notMoving)
            {
                StuckTime += Time.deltaTime;
            }
            else
            {
                StuckTime = 0.0f;
            }

            Stuck = StuckTime > 1.2f;
        }

        public void GetSteering()
        {
            float lookAheadDistance = Mathf.Lerp(15f, 40f, currentSpeed / maxSpeed);

            lookAheadDistance *= Mathf.Lerp(1f, 0.45f, curveFactor);

            //
            if (sharpTurnAhead)
            {
                lookAheadDistance *= 0.6f;
            }
            else
            {
                lookAheadDistance *= 1.15f;
            }

            float targetS = currentDistance + lookAheadDistance;
            targetS %= totalDistance;

            targetPosition = PositionAtDistance(targetS);

            //Any different from pathForward?
            //Vector3 pathForwardv2 = (PositionAtDistance(targetS + 1f) - PositionAtDistance(targetS)).normalized;

            Vector3 targetDirection = (targetPosition - thisTransform.position).normalized;
            Vector3 pathDirNow = pathPoints[bestSegment].forward;
            Vector3 blendDir = Vector3.Slerp(pathDirNow, targetDirection, 0.6f);
            //Might go back on all of this tomorrow
            //Vector3 flatVelocity = Vector3.ProjectOnPlane(rigidBody.linearVelocity, Vector3.up);
            //Vector3 velocityDirection = flatVelocity.sqrMagnitude > 0.5f ? flatVelocity.normalized : thisTransform.forward;

            //float slipAngle = Vector3.Angle(thisTransform.forward, velocityDirection);

            //float slideFactor = Mathf.InverseLerp(5f, 30f, slipAngle);
            //float velocityBlend = slideFactor * 0.4f;

            //Vector3 steeringDir = Vector3.Slerp(targetDirection, velocityDirection, velocityBlend);

            //steeringDir = very safe
            //targetDirection = safe
            //blendDir = least safe for now
            signedAngle = Vector3.SignedAngle(thisTransform.forward, blendDir, Vector3.up);

            float maxSteer = 30f;

            steerInput = Mathf.Clamp(signedAngle / maxSteer, -1f, 1f);

            float speedFactor = Mathf.Clamp01(30f / Mathf.Max(currentSpeed, 0.1f));
            steerInput *= speedFactor;

            float anticipationNoise = UnityEngine.Random.Range(-2f, 2f);
            steerInput += anticipationNoise * 0.02f * Mathf.Clamp01(10f / currentSpeed);

            float lateralFactor = Mathf.Clamp01(1f - Mathf.Abs(lateralSpeed) / 5f);
            steerInput *= lateralFactor;

            if (boxedIn)
            {
                steerInput *= 0.8f;
            }

            if (brakingHard)
            {
                steerInput *= 0.6f;
            }
        }

        public void ThrottleOrBrake()
        {
            ShouldBoost = false;

            float safeSpeed = Mathf.Sqrt(maxLatAccel / Mathf.Max(averageCurvature, 0.001f));

            float brakingDistance = EstimateBrakingDistance(currentSpeed, safeSpeed);
            distanceToTurn = 30f;

            if (sharpTurnAhead)
            {
                //safeSpeed = Mathf.Min(safeSpeed, maxSpeed * 0.7f);
                safeSpeed *= 0.75f;
                distanceToTurn = Mathf.Lerp(20f, 60f, averageCurvature / 0.08f);
            }


            //brakingHard = sharpTurnAhead && currentSpeed > safeSpeed * 1.1f;

            float widthSpeedMultiplier = Mathf.Lerp(0.9f, 1.05f, widthFactor);
            safeSpeed *= widthSpeedMultiplier;

            float agression;
            if (carAhead.Distance > 15f)
            {
                agression = 1.15f;
            }
            else
            {
                agression = Mathf.Lerp(1f, 1.15f, carAhead.Distance / 10f);
            }
            safeSpeed *= agression;

            if (canOvertake)
            {
                //float boostAmount = Mathf.Lerp(1.05f, 1.15f, widthFactor);
                //safeSpeed *= boostAmount;
                safeSpeed *= 1.15f;
                ShouldBoost = true;
            }

            float speedError = safeSpeed - currentSpeed;
            float desiredThrottle;

            //maybe lower
            float throttleGain = 0.5f;

            if (speedError > 0)
            {
                desiredThrottle = Mathf.Clamp01(speedError * throttleGain);
                brake = false;
            }
            else
            {
                desiredThrottle = 0;
                float brakeForce = Mathf.Clamp01((currentSpeed - safeSpeed) / 10f);
                brake = brakeForce > 0.05f;
            }

            if (brakingDistance > distanceToTurn)
            {
                desiredThrottle = 0;
                float brakeForce = Mathf.Clamp01((currentSpeed - safeSpeed) / 10f);
                //float brakeFactor = Mathf.Clamp01((brakingDistance - distanceToTurn) / 10f);
                //desiredThrottle = Mathf.Clamp01(desiredThrottle * (1f - brakeFactor));
                brake = brakeForce > 0.05f;
            }

            //make public
            float throttleResponse = sharpTurnAhead ? 5.0f : 7.0f;
            throttle = Mathf.MoveTowards(throttle, desiredThrottle, throttleResponse * Time.deltaTime);

            float angularSpeed = rigidBody.angularVelocity.y;
            float stabilityDivisor = sharpTurnAhead ? 2f : 4f;
            float stabilityFactor = Mathf.Clamp01(1f - Mathf.Abs(angularSpeed) / stabilityDivisor);
            throttle *= stabilityFactor;
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

            float avoidanceOffest = 0f;
            if (!canOvertake && carAhead.ForwardDot > 0.3f && carAhead.Distance < 12f)
            {
                avoidanceOffest += Mathf.Sign(carAhead.LateralDot) * -1f * Mathf.Lerp(1f, 0f, carAhead.Distance / 12f);
                desiredOffset += avoidanceOffest * halfWidth * 0.4f;
            }

            if (canOvertake && carAhead.ForwardDot > 0.6f && carAhead.Distance < 15f)
            {
                float overTakeSide = -Mathf.Sign(carAhead.LateralDot);
                float overTakeStrength = Mathf.Lerp(0.2f, 0.4f, widthFactor);
                desiredOffset += overTakeSide * halfWidth * overTakeStrength;
            }

            if (!canOvertake)
            {
                float sideBias = 0f;
                float maxDist = Mathf.Lerp(5f, 7f, currentSpeed / maxSpeed);
                float maxLat = halfWidth * 0.25f;
                sideBias += SideAvoidance(carLeft, maxDist, maxLat);
                sideBias += SideAvoidance(carRight, maxDist, maxLat);
                //if(carLeft.Distance < 5f && carRight.Distance < 5f)
                //{
                //    sideBias *= 1.5f;
                //}
                desiredOffset += sideBias;
            }

            if (canOvertake)
            {
                if (overTakeSide == 0f)
                {
                    overTakeSide = Mathf.Abs(carAhead.LateralDot) < 0.1f ? (UnityEngine.Random.value > 0.5f ? 1f : -1f) : -Mathf.Sign(carAhead.LateralDot);
                }
                float strength = Mathf.Lerp(0.3f, 0.6f, widthFactor);
                desiredOffset += overTakeSide * halfWidth * strength;
            }
            else
            {
                overTakeSide = 0f;
            }


            if (boxedIn)
            {
                desiredOffset *= 0.5f;
            }
            if (!sharpTurnAhead || distanceToTurn > 20f)
            {
                desiredOffset *= 0.3f;
            }
            if (Mathf.Abs(lateralOffset) > halfWidth)
            {
                desiredOffset = 0f;
            }

            //make public
            float offsetResponse = Mathf.Lerp(0.5f, 0.2f, currentSpeed / maxSpeed);
            currentOffset = Mathf.MoveTowards(currentOffset, desiredOffset, 2.5f * Time.deltaTime);

            targetPosition += pathRight * currentOffset;
        }

        public float WidthFactor()
        {
            float widthAhead = 0f;
            int widthSamples = 3;
            int segIdx = 0;

            for (int i = 0; i < widthSamples; i++)
            {
                if ((bestSegment + i) >= n - 1)
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

            for (int i = 0; i < n - 1; i++)
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

                if (distSq < bestDistSq)
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
                if (s <= CumulativeLength[i + 1])
                {
                    float t = (s - CumulativeLength[i]) / SegmentLength[i];
                    return Vector3.Lerp(CenterLine[i], CenterLine[i + 1], t);
                }

            }
            return CenterLine[n - 1];
        }

        public float EstimateCurvature()
        {
            float lookAhead = sharpTurnAhead ? 40f : 15f;
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

        float SideAvoidance(CarContexts car, float maxDist, float maxLat)
        {
            if (Mathf.Abs(car.ForwardDot) > 0.6f)
            {
                return 0f;
            }

            if (car.Distance > maxDist)
            {
                return 0f;
            }

            float distFactor = 1f - (car.Distance / maxDist);
            float sideSign = -Mathf.Sign(car.LateralDot);

            return sideSign * distFactor * maxLat;
        }
    }
}
