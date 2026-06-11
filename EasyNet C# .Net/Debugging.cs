namespace EasyNet_Debugging
{
    public static class debug
    {
        public static DebugMode debugmode;
        public static bool Log()
        {
            if (debugmode == DebugMode.Logs || debugmode == DebugMode.LogsAndWarnings || debugmode == DebugMode.All)
                return true;
            else
                return false;
        }
        public static bool Warning()
        {
            if (debugmode == DebugMode.Warnings || debugmode == DebugMode.LogsAndWarnings || debugmode == DebugMode.All || debugmode == DebugMode.WarningsAndErrors)
                return true;
            else
                return false;
        }
        public static bool Error()
        {
            if (debugmode == DebugMode.Errors || debugmode == DebugMode.WarningsAndErrors || debugmode == DebugMode.All)
                return true;
            else
                return false;
        }
    }
    public enum DebugMode
    {
        /// <summary>
        /// Shows nothing for Networking debug logs
        /// </summary>
        None,
        /// <summary>
        /// Only shows basic logs
        /// </summary>
        Logs,
        /// <summary>
        /// Shows just warnings
        /// </summary>
        Warnings,
        /// <summary>
        /// Shows logs and warnings
        /// </summary>
        LogsAndWarnings,
        /// <summary>
        /// Shows just errors
        /// </summary>
        Errors,
        /// <summary>
        /// Shows warnings and errors
        /// </summary>
        WarningsAndErrors,
        /// <summary>
        /// Shows everything... and floods your console.
        /// </summary>
        All
    }

}