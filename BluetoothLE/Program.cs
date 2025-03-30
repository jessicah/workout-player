using BluetoothLE.Components;
using Blazor.Bluetooth;
using Microsoft.Extensions.FileProviders;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using BluetoothLE.Services;
using BluetoothLE.Utilities;

namespace BluetoothLE
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddBluetoothNavigator();

            builder.Services.AddDbContextFactory<Models.AthleteContext>();

            builder.Services.AddQuickGridEntityFrameworkAdapter();

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddSingleton<IMemoryCache, MemoryCache>();

            builder.Services.AddSingleton<Utilities.StravaOAuth>();
            builder.Services.AddSingleton<Utilities.IntervalsUploader>();
            builder.Services.AddScoped<Utilities.BluetoothHandler>();

            builder.Services.AddSingleton<SufferService>();

            builder.Services.AddScoped<HttpLogger>();
            builder.Services.AddHttpClient("my-client", client =>
            {

            }).AddLogger<BluetoothLE.Utilities.HttpLogger>(wrapHandlersPipeline: true);

            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddFilter("System.Net.Http.HttpClient", LogLevel.Trace);
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseMigrationsEndPoint();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            //app.UseStaticFiles(new StaticFileOptions
            //{
            //    FileProvider = new PhysicalFileProvider(app.Configuration.GetValue<string>("VideosPath")),
            //    RequestPath = "/videos",
            //    ServeUnknownFileTypes = true,
            //    DefaultContentType = "video/webm"
            //});

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(@"G:\Content"),
                RequestPath = "/videos",
                ServeUnknownFileTypes = true,
                DefaultContentType = "video/mp4"
            });

            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
