using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TwitterStreaming
{
    static class Bootstrap
    {
        public static async Task Main()
        {
            var version = typeof(Bootstrap).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Log.WriteInfo($"Version: {version} - Runtime: {RuntimeInformation.FrameworkDescription}");

            var expander = new TwitterStreaming();
            await expander.Initialize();
            await expander.StartTwitterStream();
        }
    }
}
