using NoSlimes.Logging;

namespace RacingGame
{
    public abstract class GameManagerState : StateMachine.State
    {
        protected GameManager GameManager { get; private set; }
        protected DLogCategory LogCategory { get; private set; }

        public GameManagerState(GameManager gameManager, DLogCategory logCategory)
        {
            GameManager = gameManager;
            LogCategory = logCategory;
        }
    }
}