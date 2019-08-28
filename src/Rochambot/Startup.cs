using System;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Rochambot
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.AddScoped<UserService>();
            services.AddScoped<GameService>();

            services.AddSingleton<JsonSerializerOptions>(new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            services.AddHttpClient<GameClient>(client =>
            {
                client.BaseAddress = new Uri(Configuration["gamemaster"] ?? "http://localhost:3500/v1.0/actions/gamemaster/");
            });
            services.AddHttpClient<MatchMakerClient>(client =>
            {
                client.BaseAddress = new Uri(Configuration["matchmaker"] ?? "http://localhost:3500/v1.0/actions/matchmaker/");
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub<App>("app");
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
