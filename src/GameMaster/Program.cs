using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;

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
                    webBuilder.UseStartup<Startup>();
                });
    }
}