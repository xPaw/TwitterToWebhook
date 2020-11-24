using System;

namespace TwitterStreaming
{
    static class Bootstrap
    {
        public static void Main()
        {
            var expander = new TwitterStreaming();
            expander.StartTwitterStream();
        }
    }
}
