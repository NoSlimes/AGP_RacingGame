using NoSlimes.Logging;
using UnityEngine;

namespace RacingGame
{
    public class PauseState : GameManagerState
    {
        private float prevTimeScale = 1f;
        public PauseState(GameManager gameManager, DLogCategory logCategory) : base(gameManager, logCategory) { }

        public override void Enter()
        {
            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            DLogger.LogDev("Game Paused.", category: LogCategory);
        }

        public override void Exit()
        {
            Time.timeScale = prevTimeScale;
            DLogger.LogDev("Game Resumed.", category: LogCategory);
        }
    }
}