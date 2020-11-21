using System;
using System.Threading.Tasks;

namespace TwitterStreaming
{
    static class Bootstrap
    {
        public static async Task Main()
        {
            var expander = new TwitterStreaming();
            await expander.StartTwitterStream();
        }
    }
}
