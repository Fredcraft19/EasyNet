namespace EasyNet_Debugging
{
    public static class debug
    {
        public static DebugMode debugmode = DebugMode.None;
        public static bool Log() => debugmode.HasFlag(DebugMode.Logs);
        public static bool Warning() => debugmode.HasFlag(DebugMode.Warnings);
        public static bool Error() => debugmode.HasFlag(DebugMode.Errors);
    }
    [Flags]
    public enum DebugMode
    {
        /// <summary>
        /// Shows no logs at all
        /// </summary>
        None = 0,
        /// <summary>
        /// Only shows basic logs
        /// </summary>
        Logs = 1 << 0,
        /// <summary>
        /// Only shows warnings
        /// </summary>
        Warnings = 1 << 1,
        /// <summary>
        /// Only shows errors
        /// </summary>
        Errors = 1 << 2, 
        /// <summary>
        /// Shows logs and warnings
        /// </summary>
        LogsAndWarnings = Logs | Warnings,
        /// <summary>
        /// Shows Warnings and errors
        /// </summary>
        WarningsAndErrors = Warnings | Errors,
        /// <summary>
        /// Shows everything (it will fill your console quickly)
        /// </summary>
        All = Logs | Warnings | Errors
    }

}
