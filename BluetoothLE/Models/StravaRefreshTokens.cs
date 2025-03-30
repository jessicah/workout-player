using System.ComponentModel.DataAnnotations;

namespace BluetoothLE.Models
{
    public class StravaRefreshTokens
    {
        public int Id { get; set; }

        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
