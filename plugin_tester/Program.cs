using System;
using BSDriverPlugin;

namespace PluginTester
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Initializing Plugin...");

            var plugin = new Plugin();

            Console.WriteLine("Plugin initialized successfully.");
            // Optionally, interact with the plugin here

            // Dispose when done
            plugin.Dispose();
        }
    }
}
