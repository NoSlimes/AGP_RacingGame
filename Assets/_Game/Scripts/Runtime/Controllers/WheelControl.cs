using RacingGame;
using UnityEngine;

[RequireComponent(typeof(WheelCollider))]
public class WheelControl : TickableBehaviour, ICarComponent
{
    [SerializeField] private Transform wheelModel;
    [field: SerializeField] public bool IsFront { get; private set; }
    [field: SerializeField] public bool Steerable { get; private set; }
    [field: SerializeField] public bool Motorized { get; private set; }

    public WheelCollider WheelCollider { get; private set; }
    public ParticleSystem skidParticles { get; private set; }


    private Vector3 position;
    private Quaternion rotation;

    private void Awake()
    {
        skidParticles = GetComponentInChildren<ParticleSystem>(true);
        if (skidParticles != null)
        {
            skidParticles.Stop();
        }
        else
        {
            Debug.LogWarning($"No ParticleSystem found for wheel: {gameObject.name}");
        }
    }

    public void Initialize(Car ownerCar)
    {
        WheelCollider = GetComponent<WheelCollider>();
    }

    public override void Tick()
    {
        if(WheelCollider == null || wheelModel == null)
            return;

        WheelCollider.GetWorldPose(out position, out rotation);
        wheelModel.transform.SetPositionAndRotation(position, rotation);
    }
}
