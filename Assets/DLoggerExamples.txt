using NoSlimes.Logging;
using NoSlimes.Util.UniTerminal;
using UnityEngine;

namespace RacingGame
{
    public class DLoggerExamples : MonoBehaviour
    {
        private static readonly DLogCategory exampleCategory = new DLogCategory("ExampleCategory", Color.red);

        private void Start()
        {
            DLogger.LogDev("This is a development log message.", this, exampleCategory);
        }

        [ConsoleCommand("log_example")]
        private static void LogExample(CommandResponseDelegate resp, string message)
        {
            DLogger.LogDev($"Console command log: {message}", null, exampleCategory);
            resp($"Logged message: {message}");
        }
    }
}
