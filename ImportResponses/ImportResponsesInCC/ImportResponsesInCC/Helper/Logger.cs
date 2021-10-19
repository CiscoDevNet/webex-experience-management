namespace CreateResponseExcel.Helper
{
    using System;
    using System.IO;
    static public class Logger
    {
        private const string FILE_EXT = ".log";
        private static readonly string datetimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private static string logFilename;

        /// Initiate an instance of Logger class constructor.
        /// Will be called before one of the logging function called.
        /// If log file does not exist, it will be created automatically.
        static Logger()
        {
            logFilename = null;

            logFilename = GenerateLogFileName();
            CreateLogFileHeader();

        }

        private static void CreateLogFileHeader()
        {
            // Log file header line
            string logHeader = logFilename + " is created.";
            if (!File.Exists(logFilename))
            {
                WriteLine(DateTime.Now.ToString(datetimeFormat) + " " + logHeader, false);
            }
        }

        private static string GenerateLogFileName()
        {
            /// Create Log Folder
            string LogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);

            string Filename = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name
                + DateTime.Now.ToString("yyyy-MM-dd")
                + FILE_EXT;

            return Path.Combine(LogDir, Filename);
        }

        /// Log a DEBUG message
        public static void Debug(string text, params string[] args)
        {
            WriteFormattedLog(LogLevel.DEBUG, text, args);
        }

        /// Log an ERROR message
        public static void Error(string text, params string[] args)
        {
            WriteFormattedLog(LogLevel.ERROR, text, args);
        }

        /// Log a FATAL ERROR message
        public static void Fatal(string text, params string[] args)
        {
            WriteFormattedLog(LogLevel.FATAL, text, args);
        }

        /// Log an INFO message
        public static void Info(string text, params string[] args)
        {
            WriteFormattedLog(LogLevel.INFO, text, args);
        }

        /// Log a TRACE message
        public static void Trace(string text, params string[] args)
        {
            WriteFormattedLog(LogLevel.TRACE, text, args);
        }

        /// Log a WARNING message
        public static void Warning(string text, params string[] args)
        {
            WriteFormattedLog(LogLevel.WARNING, text, args);
        }

        private static void WriteLine(string text, bool append = true)
        {
            try
            {
                PerformLogRotation();

                using (FileStream fs = new FileStream(logFilename, FileMode.Append, FileAccess.Write,
                        FileShare.Write, 4096, FileOptions.None))
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        writer.WriteLine(text);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        private static void PerformLogRotation()
        {
            string tempLog = GenerateLogFileName();
            if (tempLog != logFilename)
            {
                logFilename = tempLog;
                CreateLogFileHeader();
            }
        }

        private static void WriteFormattedLog(LogLevel level, string text, params string[] args)
        {
            string pretext;
            switch (level)
            {
                case LogLevel.TRACE:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [TRACE]   ";
                    break;
                case LogLevel.INFO:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [INFO]    ";
                    break;
                case LogLevel.DEBUG:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [DEBUG]   ";
                    break;
                case LogLevel.WARNING:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [WARNING] ";
                    break;
                case LogLevel.ERROR:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [ERROR]   ";
                    break;
                case LogLevel.FATAL:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [FATAL]   ";
                    break;
                default:
                    pretext = "";
                    break;
            }

            if (args.Length > 0)
            {
                text += " - " + args[0];
                for (int i = 1; i < args.Length; i++)
                {
                    text += ", " + args[i];
                }
            }

            WriteLine(pretext + text);
        }

        [System.Flags]
        private enum LogLevel
        {
            TRACE,
            INFO,
            DEBUG,
            WARNING,
            ERROR,
            FATAL
        }
    }
}