using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RacingGame
{
    public class AICarController : MonoBehaviour, ICarInputs
    {
        public Vector2 MoveInput { get; private set; }
        public bool BrakeInput { get; private set; }
        public bool NitroInput { get; private set; }

        private PlayerCarController playerCar;
        private Vector3 targetWP;

        [SerializeField]
        private PCGManager pCG;

        private List<Vector3> wayPoints;
        private int currentindex = 0;
        private float maxSpeed = 50;
        private float currentSpeed = 20;

        void Start()
        { 
            if(pCG)
            {
                wayPoints = pCG.Centerline;
            }
            
            playerCar = FindAnyObjectByType<PlayerCarController>();

            transform.position = wayPoints[currentindex];
            currentindex++;
        }

        private void Update()
        {
            //MoveInput = new Vector2(UnityEngine.Random.Range(-1f, 1f), 1f);
            //BrakeInput = UnityEngine.Random.value < 0.1f;

            //just testing a lot
            targetWP = wayPoints[currentindex];

            Vector3 dirToMovePosition = (targetWP - transform.position).normalized;
            MoveInput = new Vector2(dirToMovePosition.x, dirToMovePosition.z);
            BrakeInput = ShouldBrake();
            NitroInput = ShouldBoost();

            //Hard coded movement, just to test the car could read all waypoints. To be removed when Car Controlls are ready.
            transform.position = Vector3.MoveTowards(transform.position, targetWP, currentSpeed * Time.deltaTime);

            //Rotation
            float singleStep = 1.0f * Time.deltaTime;
            Vector3 newDir = Vector3.RotateTowards(transform.forward, dirToMovePosition, singleStep, 0.0f);
            transform.rotation = Quaternion.LookRotation(newDir);

            //Change Waypoint Index
            float dot = Vector3.Dot(transform.forward, dirToMovePosition);
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
