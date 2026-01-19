namespace RacingGame
{
    public interface ITickable
    {
        public virtual int Priority => 0;

        public void Tick() { }
        public void LateTick() { }
        public void FixedTick() { }
    }
}
