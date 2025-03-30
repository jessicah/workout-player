using BluetoothLE;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Migrations
{
    internal class Program
    {
        static void Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddDbContextFactory<BluetoothLE.Models.AthleteContext>();
            builder.Services.AddSingleton<StateContainer>(new StateContainer());

            IHost host = builder.Build();

            host.Run();
        }
    }
}
