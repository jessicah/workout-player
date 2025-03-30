using BluetoothLE.Services;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Blazor.Bluetooth;
using System.Resources;

namespace Win32
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            var services = new ServiceCollection();

            services.AddWindowsFormsBlazorWebView();
            //services.AddRazorComponents()
              //  .AddInteractiveServerComponents();

            services.AddBluetoothNavigator();
            services.AddDbContextFactory<BluetoothLE.Models.AthleteContext>();

            services.AddQuickGridEntityFrameworkAdapter();

            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddSingleton<IMemoryCache, MemoryCache>();

            services.AddSingleton<BluetoothLE.Utilities.StravaOAuth>();
            services.AddScoped<BluetoothLE.Utilities.BluetoothHandler>();

            services.AddSingleton<SufferService>();

            services.AddHttpClient();

            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json");

            services.AddScoped<IConfiguration>(_ => configurationBuilder.Build());

            blazorWebView1.HostPage = @"wwwroot\index.html";
            blazorWebView1.Services = services.BuildServiceProvider();
            blazorWebView1.RootComponents.Add<BluetoothLE.Components.App>("#app");
        }
    }
}
