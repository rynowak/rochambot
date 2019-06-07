using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace GameMaster
{
    public class Program
    {
        public static void Main(string[] args) => 
            CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddAzureAppConfiguration(options => 
                    {
                        var cnStr = hostingContext.Configuration["AzureAppConfigConnectionString"];
                        options.Connect(cnStr);
                    });
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options => 
                    {
                        options.Listen(IPAddress.Any, 50051, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                        });
                    });

                    webBuilder.UseStartup<Startup>();
                });
    }
}