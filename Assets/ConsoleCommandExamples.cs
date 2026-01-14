using NoSlimes.Util.UniTerminal;
using UnityEngine;

namespace RacingGame
{
    public class ConsoleCommandExamples : MonoBehaviour
    {
        [ConsoleCommand("test_bool")]
        private static void TestBool(CommandResponseDelegate resp, bool test123)
        {
            resp($"TestBool called with argument: {test123}");
        }

        public enum TestEnum
        {
            OptionA,
            OptionB,
            OptionC
        }

        [ConsoleCommand("test_enum")]
        private static void TestEnumCommand(CommandResponseDelegate resp, TestEnum option)
        {
            resp($"TestEnumCommand called with argument: {option}");
        }

        [ConsoleCommand("test_both")]
        private static void TestBoth(CommandResponseDelegate resp, bool flag, TestEnum option)
        {
            resp($"TestBoth called with arguments: flag={flag}, option={option}");
        }
    }
}
