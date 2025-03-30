using Microsoft.EntityFrameworkCore;

namespace BluetoothLE.Models
{
    public class AthleteContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(@"Data Source=C:\Users\jessi\source\repos\BluetoothLE\BluetoothLE\Data\Athletes.db");
        }

        public DbSet<Models.Stats> Stats { get; set; }

        public DbSet<Models.StravaAccessTokens> AccessTokens { get; set; }

        public DbSet<Models.StravaRefreshTokens> RefreshTokens { get; set; }
    }
}
