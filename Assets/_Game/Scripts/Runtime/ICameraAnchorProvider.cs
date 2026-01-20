using UnityEngine;

namespace RacingGame
{
    public interface ICameraAnchorProvider
    {
        public Transform CameraAnchor { get; }
    }

    public class CarCameraAnchor : TickableBehaviour, ICameraAnchorProvider
    {
        public Transform CameraAnchor { get; private set;  }

        //[SerializeField] 

    }
}
