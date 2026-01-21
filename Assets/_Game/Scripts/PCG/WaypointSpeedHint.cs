using UnityEngine;

namespace RacingGame
{
    public enum WaypointZoneType
    {
        Accelerate,
        Cruise,
        Brake
    }

    public class WaypointSpeedHint : MonoBehaviour
    {
        [Header("Geometry")] 
        public float turnAngleDeg; // angle between segments
        public float curvature; // approx 1/radius
        public float radius; // meters

        [Header("Speed Hint")] 
        public float recommendedSpeed; // m/s
        public WaypointZoneType zone;

        [Header("Optional Debug")] 
        public float slopeDeg; // uphill/downhill at this segment
    }
}