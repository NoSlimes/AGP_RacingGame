using NoSlimes.Util.UniTerminal;
using UnityEngine;

namespace RacingGame
{
    public class Testesrst : MonoBehaviour
    {
        [ConsoleCommand("test_bool")]
        private static void TestBool(bool test123)
        {
            Debug.Log($"TestBool called with argument: {test123}");
        }

        public enum TestEnum
        {
            OptionA,
            OptionB,
            OptionC
        }

        [ConsoleCommand("test_enum")]
        private static void TestEnumCommand(TestEnum option)
        {
            Debug.Log($"TestEnumCommand called with argument: {option}");
        }

        [ConsoleCommand("test_both")]
        private static void TestBoth(bool flag, TestEnum option)
        {
            Debug.Log($"TestBoth called with arguments: flag={flag}, option={option}");
        }
    }
}
