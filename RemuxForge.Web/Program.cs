using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RemuxForge.Web.Components;
using RemuxForge.Web.Services;
using RemuxForge.Core;

namespace RemuxForge.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            int port = 5000;
            string envPort = Environment.GetEnvironmentVariable("REMUXFORGE_PORT");

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
            AppSettingsService.Instance.Initialize();

            // Auto-find tool (mkvmerge, ffmpeg, mediainfo)
            bool toolsChanged = false;

            if (AppSettingsService.Instance.Settings.Tools.MkvMergePath.Length == 0 || AppSettingsService.Instance.Settings.Tools.MkvMergePath == "mkvmerge" || !System.IO.File.Exists(AppSettingsService.Instance.Settings.Tools.MkvMergePath))
            {
                MkvMergeProvider mkvProvider = new MkvMergeProvider();
                if (mkvProvider.Resolve(false))
                {
                    AppSettingsService.Instance.Settings.Tools.MkvMergePath = mkvProvider.MkvMergePath;
                    toolsChanged = true;
                }
            }

            if (AppSettingsService.Instance.Settings.Tools.FfmpegPath.Length == 0 || !System.IO.File.Exists(AppSettingsService.Instance.Settings.Tools.FfmpegPath))
            {
                FfmpegProvider ffProvider = new FfmpegProvider(AppSettingsService.Instance.ConfigFolder);
                if (ffProvider.Resolve(false, false))
                {
                    AppSettingsService.Instance.Settings.Tools.FfmpegPath = ffProvider.FfmpegPath;
                    toolsChanged = true;
                }
            }

            if (AppSettingsService.Instance.Settings.Tools.MediaInfoPath.Length == 0 || !System.IO.File.Exists(AppSettingsService.Instance.Settings.Tools.MediaInfoPath))
            {
                MediaInfoProvider miProvider = new MediaInfoProvider();
                if (miProvider.Resolve(false))
                {
                    AppSettingsService.Instance.Settings.Tools.MediaInfoPath = miProvider.MediaInfoPath;
                    toolsChanged = true;
                }
            }

            if (toolsChanged)
            {
                AppSettingsService.Instance.Save();
            }

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
