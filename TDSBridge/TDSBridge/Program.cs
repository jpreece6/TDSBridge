using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TDSBridge.Common;

namespace TDSBridge
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
            await Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    
                })
                .ConfigureServices((hc, services) =>
                {
                    services.AddMemoryCache();
                    services.AddHostedService<ServerService>();
                    services.AddOptions<ServerSettings>().Bind(hc.Configuration.GetSection("ServerSettings"));
                })
                .RunConsoleAsync();
        }

        static void Usage()
        {
            Console.WriteLine(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " <listen port> <sql server address> <sql server port>");
        }
    }
}
