using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TDSBridge.Common.Cache;

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
                    services.UseRedis(hc.Configuration);
                    //services.UseInMemoryCache(); 
                    services.AddHostedService<ServerService>();
                    services.AddOptions<ServerSettings>().Bind(hc.Configuration.GetSection("ServerSettings"));

                    services.AddHostedService<WriteBackService>();
                })
                .RunConsoleAsync();
        }
    }
}
