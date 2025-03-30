namespace BluetoothLE.Models
{
    public class Lap
    {
        public record struct Item(int HeartRate, int Cadence, int Power);

        public record LapSummary(int AvgHeartRate, int MaxHeartRate, int AvgCadence, int MaxCadence, int AvgPower, int MaxPower)
        {
            public static LapSummary Zero = new(0, 0, 0, 0, 0, 0);
        }

        public record LapsSummary(int AvgHeartRate, int MaxHeartRate, int MinHeartRate, int AvgCadence, int MaxCadence, int AvgPower, int MaxPower)
        {
            public static LapsSummary Zero = new(0, 0, 0, 0, 0, 0, 0);
        }

        public Queue<Item> Data { get; } = [];

        public DateTime StartTime { get; init; }
        public DateTime? FinishTime { get; set; }

        public Lap(DateTime startedAt, int estimatedCapacity)
        {
            StartTime = startedAt;

            Data.EnsureCapacity(estimatedCapacity);
        }

        public void Add(int heartRate, int cadence, int power)
        {
            Data.Enqueue(new Item(heartRate, cadence, power));

            if (heartRate > 0)
            {
                ++HRSamples;

                HeartRateCumulativeAverage = (HeartRateCumulativeAverage * (HRSamples - 1) + (double)heartRate) / HRSamples;
            }

            if (cadence > 0)
            {
                ++CadenceSamples;

                CadenceCumulativeAverage = (CadenceCumulativeAverage * (CadenceSamples - 1) + (double)cadence) / CadenceSamples;
            }

            ++PowerSamples;
            PowerCumulativeAverage = (PowerCumulativeAverage * (PowerSamples - 1) + (double)power) / PowerSamples;
        }

        public void EndLap(DateTime finishedAt)
        {
            FinishTime = finishedAt;
        }

        public double HeartRateCumulativeAverage = 0;
        public double CadenceCumulativeAverage = 0;
        public double PowerCumulativeAverage = 0;

        public int HRSamples = 0;
        public int CadenceSamples = 0;
        public int PowerSamples = 0;

        public LapSummary Summary()
        {
            if (Data.Count == 0)
                return LapSummary.Zero;

            int heartRateAverage = 0;
            int cadenceAverage = 0;
            int powerAverage = 0;

            int heartRateMax = 0;
            int cadenceMax = 0;
            int powerMax = 0;

            /* don't need double precision, FIT file only stores integers */
            if (Data.Any(item => item.HeartRate > 0))
                heartRateAverage = (int)Data.Where(item => item.HeartRate > 0).Average(item => item.HeartRate);
            if (Data.Any(item => item.Cadence > 0))
                cadenceAverage = (int)Data.Where(item => item.Cadence > 0).Average(item => item.Cadence);
            powerAverage = (int)Data.Average(item => item.Power);

            heartRateMax = Data.Max(item => item.HeartRate);
            cadenceMax = Data.Max(item => item.Cadence);
            powerMax = Data.Max(item => item.Power);

            if ((int)HeartRateCumulativeAverage != heartRateAverage)
            {
                Console.WriteLine($"HR average mismatch: {(int)HeartRateCumulativeAverage} != {heartRateAverage}");
            }
            else
            {
                Console.WriteLine($"HR average match! OK");
            }
            if ((int)CadenceCumulativeAverage != cadenceAverage)
            {
                Console.WriteLine($"Cadence average mismatch: {(int)CadenceCumulativeAverage} != {cadenceAverage}");
            }
            else
            {
                Console.WriteLine($"Cadence average match! OK");
            }
            if ((int)PowerCumulativeAverage != powerAverage)
            {
                Console.WriteLine($"Power average mismatch: {(int)PowerCumulativeAverage} != {powerAverage}");
            }
            else
            {
                Console.WriteLine($"Power average match! OK");
            }

                return new(heartRateAverage, heartRateMax, cadenceAverage, cadenceMax, powerAverage, powerMax);
        }

        public static LapsSummary TotalSummary(IEnumerable<Lap> laps)
        {
            if (laps.Select(lap => lap.Data.Count).Sum() == 0)
            {
                return LapsSummary.Zero;
            }

            int heartRateAverage = 0;
            int cadenceAverage = 0;
            int powerAverage = 0;

            int heartRateMax = 0;
            int cadenceMax = 0;
            int powerMax = 0;

            int heartRateMin = 0;

            var heartRate = laps.SelectMany(lap => lap.Data).Where(item => item.HeartRate > 0).Select(item => item.HeartRate);
            var cadence = laps.SelectMany(lap => lap.Data).Where(item => item.Cadence > 0).Select(item => item.Cadence);
            var power = laps.SelectMany(lap => lap.Data).Select(item => item.Power);

            /* don't need double precision, FIT file only stores integers */
            if (heartRate.Count() > 0)
            {
                heartRateAverage = (int)heartRate.Average();
                heartRateMax = (int)heartRate.Max();
                heartRateMin = (int)heartRate.Min();
            }

            if (cadence.Count() > 0)
            {
                cadenceAverage = (int)cadence.Average();
                cadenceMax = (int)cadence.Max();
            }

            if (power.Count() > 0)
            {
                powerAverage = (int)power.Average();
                powerMax = (int)power.Max();
            }
            else
            {
                powerAverage = 0;
                powerMax = 0;
            }

            return new(heartRateAverage, heartRateMax, heartRateMin, cadenceAverage, cadenceMax, powerAverage, powerMax);
        }
    }
}
