using System;
using System.IO;

namespace TwitterStreaming
{
    static class Log
    {
        private enum Category
        {
            DEBUG,
            INFO,
            WARN,
            ERROR
        }

        private static readonly object logLock = new object();

        public static void WriteDebug(string component, string format, params object[] args) => WriteLine(Category.DEBUG, component, format, args);
        public static void WriteInfo(string component, string format, params object[] args) => WriteLine(Category.INFO, component, format, args);
        public static void WriteWarn(string component, string format, params object[] args) => WriteLine(Category.WARN, component, format, args);
        public static void WriteError(string component, string format, params object[] args) => WriteLine(Category.ERROR, component, format, args);

        private static void WriteLine(Category category, string component, string format, params object[] args)
        {
            var logLine = $"{DateTime.Now.ToString("s").Replace('T', ' ')} [{category}] {component}: {(args.Length > 0 ? string.Format(format, args) : format)}{Environment.NewLine}";

            lock (logLock)
            {
                if (category == Category.ERROR)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(logLine);
                    Console.ResetColor();
                }
                else if (category == Category.DEBUG)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(logLine);
                    Console.ResetColor();
                }
                else if (category == Category.WARN)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write(logLine);
                    Console.ResetColor();
                }
                else
                {
                    Console.Write(logLine);
                }
            }
        }
    }
}
