namespace RacingGame
{
    public interface ICarComponent
    {
        public virtual int Priority => 0;

        void Initialize(Car ownerCar);

        void TickComponent() { }
        void FixedTickComponent() { }
    }
}
