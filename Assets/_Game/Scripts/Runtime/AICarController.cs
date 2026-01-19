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
        private float maxSpeed = 5;
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
            MoveInput = new Vector2(UnityEngine.Random.Range(-1f, 1f), 1f);
            BrakeInput = UnityEngine.Random.value < 0.1f;

            //just testing a lot
            targetWP = wayPoints[currentindex];
            Vector3 dirToMovePosition = (targetWP - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dirToMovePosition);

            float singleStep = 1.0f * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetWP, currentSpeed * Time.deltaTime);
            Vector3 newDir = Vector3.RotateTowards(transform.position, dirToMovePosition, singleStep, 0.0f);
            //transform.rotation = Quaternion.LookRotation(newDir);

            if (dot <= 0)
            {
                currentindex++;
                Debug.Log(currentindex);
                if (currentindex >= wayPoints.Count)
                {
                    currentindex = 0;
                }
            }

        }
    }
}
