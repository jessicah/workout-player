using System.ComponentModel.DataAnnotations;

namespace BluetoothLE.Models
{
    public class StravaAccessTokens
    {
        public int Id { get; set; }

        [Required]
        public string AccessToken { get; set; } = null!;

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.MinValue;
    }
}
