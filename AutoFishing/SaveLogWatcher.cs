using System;
using System.Collections.Generic;
using Koturn.VRChat.Log;
using Koturn.VRChat.Log.Enums;


namespace AutoFishing
{
    /// <summary>
    /// Output all logs to stdout.
    /// </summary>
    public sealed class SaveLogWatcher : VRCBaseLogWatcher
    {
        /// <summary>
        /// Occurs when detect SAVE log.
        /// </summary>
        public event EventHandler<EventArgs>? DataSaved;

        /// <summary>
        /// Create an instance of <see cref="VRCBaseLogParser"/>.
        /// </summary>
        /// <param name="filePath">File path to log file.</param>
        /// <returns>An instance of <see cref="VRCBaseLogParser"/>.</returns>
        protected override VRCBaseLogParser CreateLogParser(string filePath)
        {
            return new SaveLogParser(this, filePath);
        }

        /// <summary>
        /// <see cref="VRCBaseLogParser"/> for <see cref="SaveLogWatcher"/>.
        /// </summary>
        /// <remarks>
        /// Primary ctor: Open specified file.
        /// </remarks>
        /// <param name="watcher">Parent <see cref="SaveLogWatcher"/>.</param>
        /// <param name="filePath">Log file path to open.</param>
        private sealed class SaveLogParser(SaveLogWatcher watcher, string filePath)
            : VRCBaseLogParser(filePath)
        {
            /// <summary>
            /// Load one log item and output to stdout.
            /// </summary>
            /// <param name="level">Log level.</param>
            /// <param name="logLines">Log lines.</param>
            /// <returns>True if any of the log parsing defined in this class succeeds, otherwise false.</returns>
            protected override bool OnLogDetected(VRCLogLevel level, List<string> logLines)
            {
                if (logLines[0] == "SAVED DATA")
                {
                    watcher.DataSaved?.Invoke(this, EventArgs.Empty);
                }

                return true;
            }
        }
    }
}
