using System;
using System.Text;
using Koturn.Windows.Consoles;


namespace AutoFishing
{
    /// <summary>
    /// Provides helper methods for console.
    /// </summary>
    public static class ConsoleEx
    {
        /// <summary>
        /// Console state.
        /// </summary>
        private static ConsoleState _consoleState;

        /// <summary>
        /// Ensure that the console is set up correctly.
        /// </summary>
        public static void EnsureSetupConsole()
        {
            if (_consoleState == ConsoleState.NotAlloced)
            {
                SetupConsole();
            }
        }

        /// <summary>
        /// Allocate console, disable close button and exit key on the console, set encoding to UTF-8.
        /// </summary>
        public static void SetupConsole()
        {
            ConsoleUtil.AllocConsole();
            ConsoleUtil.DisableCloseButton();
            ConsoleUtil.DisableExitKeys();
            Console.OutputEncoding = Encoding.UTF8;
            _consoleState = ConsoleState.Show;
        }

        /// <summary>
        /// Ensure current console state.
        /// </summary>
        public static void EnsureCurrentConsoleState()
        {
            if (_consoleState == ConsoleState.Show)
            {
                ConsoleUtil.ShowConsole();
            }
            else if (_consoleState == ConsoleState.Hide)
            {
                ConsoleUtil.HideConsole();
            }
        }

        /// <summary>
        /// Allocate console.
        /// </summary>
        public static void NewConsole()
        {
            if (_consoleState != ConsoleState.NotAlloced)
            {
                ConsoleUtil.FreeConsole();
                ConsoleUtil.AllocConsole();
                _consoleState = ConsoleState.Show;
            }
        }

        /// <summary>
        /// Toggle console.
        /// </summary>
        public static void ToggleConsole()
        {
            switch (_consoleState)
            {
                case ConsoleState.NotAlloced:
                    SetupConsole();
                    break;
                case ConsoleState.Show:
                    ConsoleUtil.HideConsole();
                    _consoleState = ConsoleState.Hide;
                    break;
                case ConsoleState.Hide:
                    ConsoleUtil.ShowConsole();
                    _consoleState = ConsoleState.Show;
                    break;
            }
        }

        /// <summary>
        /// Write message to stdout with timestamp.
        /// </summary>
        /// <param name="message">Message to write.</param>
        public static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        }

        /// <summary>
        /// Console state values.
        /// </summary>
        private enum ConsoleState
        {
            /// <summary>
            /// Console is not allocated.
            /// </summary>
            NotAlloced,
            /// <summary>
            /// Console is shown.
            /// </summary>
            Show,
            /// <summary>
            /// Console is hidden.
            /// </summary>
            Hide
        }
    }
}
