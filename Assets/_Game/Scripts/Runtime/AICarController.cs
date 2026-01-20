using NUnit.Framework;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

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
        public bool BrakeInput { get; private set; }
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
        private Transform thisTransform;
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
            //playerCar = FindAnyObjectByType<PlayerCarInputs>();
            thisTransform = transform;

            Rigidbody rb = thisTransform.GetComponent<Rigidbody>();
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

            GameManager.Instance.RegisterTickable(this);
            
            //thisTransform.position = CenterLine[currentindex];
            //currentindex++;
        }

        public void Deinitialize()
        { }

        public void Tick()
        {
            //just testing a lot
            //targetWP = CenterLine[currentindex];

            FindClosestSegment();
            currentDistance = CumulativeLength[bestSegment] + bestT * SegmentLength[bestSegment];

            float progress = currentDistance / totalDistance;

            Vector3 pathforward = pathPoints[bestSegment].forward;
            Vector3 pathRight = Vector3.Cross(Vector3.up, pathforward);
            Vector3 toCar = thisTransform.position - bestClosest;
            float lateralOffset = Vector3.Dot(toCar, pathRight);

            //what is recoveryDistance?
            Vector3 recoveryTarget = bestClosest + pathforward * 10;

            //what is lookAheadDistance?
            float targetS = currentDistance + 10;
            targetS %= totalDistance;

            Vector3 targetPosition = PositionAtDistance(targetS);

            

            Vector3 dirToMovePosition = (targetPosition - thisTransform.position).normalized;

            float signedAngle = Vector3.SignedAngle(thisTransform.forward, dirToMovePosition, Vector3.up);

            float maxSteer = 30f;

            float steerinput = Mathf.Clamp(signedAngle / maxSteer, -1f, 1f);

            MoveInput = new Vector2(steerinput, 0.5f);
            //MoveInput = new Vector2(dirToMovePosition.x, dirToMovePosition.z);
            BrakeInput = ShouldBrake();
            NitroInput = ShouldBoost();

            //Gizmos.color = Color.red;
            //Gizmos.DrawSphere(bestClosest, 0.3f);
            //Gizmos.color = Color.green;
            //Gizmos.DrawSphere(targetPosition, 0.3f);
            //Hard coded movement, just to test the car could read all waypoints. To be removed when Car Controlls are ready.
            //thisTransform.position = Vector3.MoveTowards(thisTransform.position, targetWP, currentSpeed * Time.deltaTime);

            //Rotation
            float singleStep = 1.0f * Time.deltaTime;
            Vector3 newDir = Vector3.RotateTowards(thisTransform.forward, dirToMovePosition, singleStep, 0.0f);
            //thisTransform.rotation = Quaternion.LookRotation(newDir);

            //Change Waypoint Index
            float dot = Vector3.Dot(thisTransform.forward, dirToMovePosition);
            if (dot <= 0)
            {
                currentindex++;
                if (currentindex >= CenterLine.Count)
                {
                    currentindex = 0;
                }
            }
        }

        bool ShouldBrake()
        {
            return false;
        }

        bool ShouldBoost()
        {
            return false;
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
    }
}
