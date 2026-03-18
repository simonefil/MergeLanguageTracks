using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MergeLanguageTracks.Web.Components;
using MergeLanguageTracks.Web.Services;
using MergeLanguageTracks.Core;

namespace MergeLanguageTracks.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            int port = 5000;
            string envPort = Environment.GetEnvironmentVariable("MLT_PORT");

            if (envPort != null)
            {
                int.TryParse(envPort, out port);
            }
            else
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--port" && i + 1 < args.Length)
                    {
                        int.TryParse(args[i + 1], out port);
                    }
                }
            }

            // Inizializza impostazioni applicazione
            AppSettings.Initialize();

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.WebHost.UseUrls("http://0.0.0.0:" + port);

            // Registra servizi
            builder.Services.AddSingleton<MergeOrchestrator>();
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();

            WebApplication app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
            }

            app.UseAntiforgery();
            app.UseStaticFiles();
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
