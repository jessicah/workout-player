using Microsoft.EntityFrameworkCore;

namespace BluetoothLE.Models
{
    public class Stats
    {
        public int Id { get; set; }
        public int Nm { get; set; }
        public int Ac { get; set; }
        public int Map { get; set; }
        public int Ftp { get; set; }
    }
}
