using NoSlimes.Logging;

namespace RacingGame
{
    public class GameState : GameManagerState
    {
        public GameState(GameManager gameManager, DLogCategory logCategory) : base(gameManager, logCategory) { }

        public override void Enter()
        {
            DLogger.LogDev("Entered GameState", category: LogCategory);
        }

        public override void Update()
        {
            GameManager.UpdateTickables();
        }

        public override void LateUpdate()
        {
            GameManager.LateUpdateTickables();
        }

        public override void FixedUpdate()
        {
            GameManager.FixedUpdateTickables();
        }
    }
}