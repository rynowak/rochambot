using System;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RobbyBot
{
    public class Program
    {
        public static void Main(string[] args) => 
            CreateHostBuilder(args).Build().Run();

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddAzureAppConfiguration(options => 
                    {
                        var cnStr = hostingContext.Configuration["AzureAppConfigConnectionString"];
                        options.Connect(cnStr);
                    });
                })
                .ConfigureServices(services =>
                    services.AddHostedService<GameRequestHandler>()
                            .AddHostedService<ShapeHandler>()
                            .AddSingleton<MoveMaker>());
    }
}