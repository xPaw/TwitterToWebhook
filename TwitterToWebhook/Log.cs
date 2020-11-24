using System;
using System.IO;

namespace TwitterStreaming
{
    static class Log
    {
        private enum Category
        {
            INFO,
            WARN,
            ERROR
        }

        private static readonly object logLock = new object();

        public static void WriteInfo(string format) => WriteLine(Category.INFO, format);
        public static void WriteWarn(string format) => WriteLine(Category.WARN, format);
        public static void WriteError(string format) => WriteLine(Category.ERROR, format);

        private static void WriteLine(Category category, string format)
        {
            var logLine = $"{DateTime.Now.ToString("s").Replace('T', ' ')} [{category}] {format}{Environment.NewLine}";

            lock (logLock)
            {
                if (category == Category.ERROR)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
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
