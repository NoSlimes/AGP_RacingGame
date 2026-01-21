namespace RacingGame
{
    public interface ITickable
    {
        public virtual int Priority => 0;

        public void Tick() { }
        public void LateTick() { }
        public void FixedTick() { }

#if DEBUG

        /// <summary>
        /// Runs in DrawGizmos to draw debug information.
        /// Surround with #DEBUG
        /// </summary>
        public void DrawDebug() { }
#endif
    }
}
