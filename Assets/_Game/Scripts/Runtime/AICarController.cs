using NUnit.Framework;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RacingGame
{
    [Serializable]
    public class AICarController : ICarInputs, ITickable
    {
        public Vector2 MoveInput { get; private set; }
        public bool BrakeInput { get; private set; }
        public bool NitroInput { get; private set; }

        //private PlayerCarInputs playerCar;
        private Vector3 targetWP;

        [SerializeField]
        private PCGManager pCG;

        private List<Vector3> wayPoints;
        private int currentindex = 0;
        private float maxSpeed = 50;
        private float currentSpeed = 20;
        private Transform thisTransform;

        public void Initialize(Transform transform)
        { 
            
            
            //playerCar = FindAnyObjectByType<PlayerCarInputs>();
            thisTransform = transform;

        }

        public void PostInitialize()
        {
            GameManager.Instance.StateMachine.GetState<GameState>().RegisterTickable(this);
            
            if (pCG)
            {
                wayPoints = pCG.Centerline;
            }
            thisTransform.position = wayPoints[currentindex];
            currentindex++;
        }

        public void Deinitialize()
        { }

        public void Tick()
        {
            //MoveInput = new Vector2(UnityEngine.Random.Range(-1f, 1f), 1f);
            //BrakeInput = UnityEngine.Random.value < 0.1f;

            //just testing a lot
            targetWP = wayPoints[currentindex];

            Vector3 dirToMovePosition = (targetWP - thisTransform.position).normalized;
            MoveInput = new Vector2(dirToMovePosition.x, dirToMovePosition.z);
            BrakeInput = ShouldBrake();
            NitroInput = ShouldBoost();

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
                if (currentindex >= wayPoints.Count)
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
    }
}
