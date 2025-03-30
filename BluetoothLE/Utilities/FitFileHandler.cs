using BluetoothLE.Models;
using Dynastream.Fit;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.JavaScript.JSType;
using DateTime = System.DateTime;

namespace BluetoothLE.Utilities
{
    static class FitExtensions
    {
        public static Dynastream.Fit.DateTime ToFit(this DateTime dateTime) => new Dynastream.Fit.DateTime(dateTime);
    }

    public sealed class FitFileHandler : IDisposable
    {
        private struct AverageMaximum
        {
            public double Average;
            public int Maximum;
            public int Samples;

            public AverageMaximum()
            {
                Average = 0;
                Maximum = 0;
                Samples = 0;
            }
        }

        public string WorkoutName { get; init; }

        private FileStream _fileStream;
        private Encode _fitEncoder;
        private int _numberOfLaps;
        private Sport _sport;
        private SubSport _subSport;
        private bool _writtenStart = false;

        private DateTime _lapStart = DateTime.UtcNow;

        private AverageMaximum[] _stats = new AverageMaximum[6];

        private const int HeartRate = 0;
        private const int Cadence = 1;
        private const int Power = 2;
        private const int LapHeartRate = 3;
        private const int LapCadence = 4;
        private const int LapPower = 5;

        private SemaphoreSlim _lock = new(1, 1);

        public FitFileHandler(string workoutName, Sport sport = Sport.Cycling, SubSport subSport = SubSport.IndoorCycling)
        {
            WorkoutName = workoutName;

            string filename = $"{DateTime.Now:yyyy-MM-dd-HH-mm} - {workoutName}.fit";

            filename = Path.GetInvalidFileNameChars().Aggregate(filename, (filename, ch) => filename.Replace(ch, '_'));

            _fileStream = new FileStream(Path.Combine(@"G:\testing", filename), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            _fitEncoder = new Encode(ProtocolVersion.V10);

            var startTime = DateTime.UtcNow;

            var m = new Dynastream.Fit.DeveloperDataIdMesg
            {
                ManufacturerId = Manufacturer.TheSufferfest
            };

            var m2 = new FieldDescriptionMesg
            {
                FitBaseTypeId = FitBaseType.Uint16,
                NativeMesgNum = MesgNum.Record,
                MessageName = "power2"
            };

            _fitEncoder.Open(_fileStream);
            _fitEncoder.Write(
            [
                new FileIdMesg
                {
                    Type = Dynastream.Fit.File.Activity,
                    Manufacturer = Manufacturer.TheSufferfest,
                    Product = 0,
                    TimeCreated = DateTime.UtcNow.ToFit()
                },
                new WorkoutMesg
                {
                    WktName = System.Text.Encoding.UTF8.GetBytes($"The Sufferfest: {workoutName}")
                },
                new SportMesg
                {
                    Sport = sport,
                    SubSport = subSport
                }
            ]);

            _sport = sport;
            _subSport = subSport;
        }

        public void AddStart(DateTime startTime)
        {
            if (!_fileStream.CanWrite) return;

            _lapStart = startTime;

            _lock.Wait();

            try
            {
                _fitEncoder.Write(new EventMesg
                {
                    Timestamp = startTime.ToFit(),
                    Event = Event.Timer,
                    EventType = EventType.Start,
                    Data = 0
                });
            }
            finally
            {
                _lock.Release();
            }
        }

        public void AddLap(Models.Lap lap, int trackDurationMs)
        {
            if (!_fileStream.CanWrite) return;

            var summary = lap.Summary();

            _lock.Wait();

            Console.WriteLine($"Added lap to FIT file:");
            Console.WriteLine($"  Timestamp:     {DateTime.UtcNow.ToShortTimeString()}");
            Console.WriteLine($"  Start Time:    {lap.StartTime.ToShortTimeString()}");
            Console.WriteLine($"  Total Elapsed: {((lap.FinishTime ?? DateTime.UtcNow) - lap.StartTime).TotalSeconds}");
            Console.WriteLine($"  Total Timer:   {TimeSpan.FromMilliseconds(trackDurationMs).TotalSeconds}");

            try
            {
                _fitEncoder.Write(new LapMesg
                {
                    Timestamp = DateTime.UtcNow.ToFit(),
                    Event = Event.Lap,
                    EventType = EventType.Stop,
                    StartTime = lap.StartTime.ToFit(),
                    TotalElapsedTime = (float)((lap.FinishTime ?? DateTime.UtcNow) - lap.StartTime).TotalSeconds,
                    TotalTimerTime = (float)TimeSpan.FromMilliseconds(trackDurationMs).TotalSeconds,
                    AvgHeartRate = (byte)summary.AvgHeartRate,
                    MaxHeartRate = (byte)summary.MaxHeartRate,
                    AvgCadence = (byte)summary.AvgCadence,
                    MaxCadence = (byte)summary.MaxCadence,
                    AvgPower = (ushort)summary.AvgPower,
                    MaxPower = (ushort)summary.MaxPower,
                    LapTrigger = LapTrigger.Time,
                    Sport = _sport,
                    SubSport = _subSport
                });
            }
            finally
            {
                _lock.Release();
            }

            ++_numberOfLaps;
        }

        // Is this better here?
        private void MarkLap()
        {
            var lapEnd = DateTime.UtcNow;

            if (!_fileStream.CanWrite) return;

            _lock.Wait();

            Console.WriteLine($"Added lap to FIT file:");
            Console.WriteLine($"  Timestamp:     {lapEnd.ToShortTimeString()}");
            Console.WriteLine($"  Start Time:    {_lapStart.ToShortTimeString()}");
            Console.WriteLine($"  Total Elapsed: {(lapEnd - _lapStart).TotalSeconds}");

            try
            {
                _fitEncoder.Write(new LapMesg
                {
                    Timestamp = DateTime.UtcNow.ToFit(),
                    Event = Event.Lap,
                    EventType = EventType.Stop,
                    StartTime = _lapStart.ToFit(),
                    TotalElapsedTime = (float)(lapEnd - _lapStart).TotalSeconds,
                    //TotalTimerTime = (float)TimeSpan.FromMilliseconds(trackDurationMs).TotalSeconds,
                    AvgHeartRate = (byte)_stats[LapHeartRate].Average,
                    MaxHeartRate = (byte)_stats[LapHeartRate].Maximum,
                    AvgCadence = (byte)_stats[LapCadence].Average,
                    MaxCadence = (byte)_stats[LapCadence].Maximum,
                    AvgPower = (ushort)_stats[LapPower].Average,
                    MaxPower = (ushort)_stats[LapPower].Maximum,
                    LapTrigger = LapTrigger.Time,
                    Sport = _sport,
                    SubSport = _subSport
                });
            }
            finally
            {
                _lock.Release();
            }

            ++_numberOfLaps;

            _lapStart = DateTime.UtcNow;
            _stats[LapHeartRate] = new();
            _stats[LapCadence] = new();
            _stats[LapPower] = new();
        }

        private static void UpdateAverageAndMaximum(ref AverageMaximum averageMaximum, int value, bool includeZeroes = false)
            => UpdateAverageAndMaximum(ref averageMaximum.Average, ref averageMaximum.Maximum, ref averageMaximum.Samples, value, includeZeroes);

        private static void UpdateAverageAndMaximum(ref double cumulativeAverage, ref int maximum, ref int numberOfSamples, int value, bool includeZeroes=false)
        {
            if (value == 0 && !includeZeroes)
                return;

            maximum = int.Max(maximum, value);

            ++numberOfSamples;
            cumulativeAverage = (cumulativeAverage * (numberOfSamples - 1) + (double)value) / numberOfSamples;
        }

        public void AddRecord(DateTime timestamp, int heartRate, int cadence, int power)
        {
            if (!_fileStream.CanWrite) return;

            if (!_writtenStart)
            {
                AddStart(timestamp);

                _writtenStart = true;
            }

            if (heartRate > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(heartRate), heartRate, $"Heart Rate {heartRate} exceeds storable value");

            if (cadence > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(cadence), cadence, $"Cadence {cadence} exceeds storable value");

            if (power > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(power), power, $"Power {power} exceeds storable value");

            UpdateAverageAndMaximum(ref _stats[LapHeartRate].Average, ref _stats[LapHeartRate].Maximum, ref _stats[LapHeartRate].Samples, heartRate);
            UpdateAverageAndMaximum(ref _stats[HeartRate].Average, ref _stats[HeartRate].Maximum, ref _stats[HeartRate].Samples, heartRate);

            UpdateAverageAndMaximum(ref _stats[LapCadence], cadence);
            UpdateAverageAndMaximum(ref _stats[Cadence], cadence);

            UpdateAverageAndMaximum(ref _stats[LapPower], power, true);
            UpdateAverageAndMaximum(ref _stats[Power], power, true);

            _lock.Wait();

            try
            {
                var m = new RecordMesg
                {
                    Timestamp = timestamp.ToFit(),
                    HeartRate = (byte)heartRate,
                    Cadence = (byte)cadence,
                    Power = (ushort)power
                };

                //m.SetFieldValue();

                _fitEncoder.Write(new RecordMesg
                {
                    Timestamp = timestamp.ToFit(),
                    HeartRate = (byte)heartRate,
                    Cadence = (byte)cadence,
                    Power = (ushort)power
                });
            }
            finally
            {
                _lock.Release();
            }
        }

        public void AddDevice(byte deviceType, ushort? manufacturer, ushort? antDeviceNumber, string name, byte? version)
        {
            if (!_fileStream.CanWrite) return;

            _lock.Wait();

            try
            {
                _fitEncoder.Write(new DeviceInfoMesg
                {
                    DeviceType = deviceType,
                    Manufacturer = manufacturer,
                    AntDeviceNumber = antDeviceNumber,
                    ProductName = System.Text.UTF8Encoding.UTF8.GetBytes(name),
                    SourceType = SourceType.BluetoothLowEnergy,
                    HardwareVersion = version
                });
            }
            finally
            {
                _lock.Release();
            }
        }

        public MemoryStream Close(DateTime startTime, DateTime finishTime, double totalTimerTime, Models.Lap.LapsSummary totalSummary)
        {
            if (!_fileStream.CanWrite) return new MemoryStream();

            // TODO: support for pausing... i.e. TotalElapsedTime < TotalTimerTime
            var elapsedTime = (finishTime - startTime).TotalSeconds;

            _fitEncoder.Write(
            [
                new SessionMesg
                {
                    Timestamp = finishTime.ToFit(),
                    Event = Event.Session,
                    EventType = EventType.Stop,
                    StartTime = startTime.ToFit(),
                    Sport = _sport,
                    SubSport = _subSport,
                    TotalElapsedTime = (float)elapsedTime,
                    TotalTimerTime = (float)totalTimerTime,
                    TotalDistance = 0,
                    AvgHeartRate = (byte)totalSummary.AvgHeartRate,
                    MaxHeartRate = (byte)totalSummary.MaxHeartRate,
                    MinHeartRate = (byte)totalSummary.MinHeartRate,
                    AvgCadence = (byte)totalSummary.AvgCadence,
                    MaxCadence = (byte)totalSummary.MaxCadence,
                    AvgPower = (ushort)totalSummary.AvgPower,
                    MaxPower = (ushort)totalSummary.MaxPower,
                    NumLaps = (ushort)_numberOfLaps
                },
                new ActivityMesg
                {
                    Timestamp = finishTime.ToFit(),
                    TotalTimerTime = (float)totalTimerTime,
                    NumSessions = 1,
                    Type = Activity.Manual,
                    EventType = EventType.Stop,
                    Event = Event.Activity
                },
                new EventMesg
                {
                    Timestamp = finishTime.ToFit(),
                    Event = Event.Timer,
                    EventType = EventType.Stop,
                    Data = 0
                },
                new EventMesg
                {
                    Timestamp = finishTime.ToFit(),
                    Event = Event.Timer,
                    EventType = EventType.StopAll,
                    Data = 0
                },
                new EventMesg
                {
                    Timestamp = finishTime.ToFit(),
                    Event = Event.Session,
                    EventType = EventType.StopDisableAll
                }
            ]);

            _fitEncoder.Close();

            // Copy the file to a memory stream, so the file can be
            // safely disposed.
            var memoryStream = new MemoryStream((int)_fileStream.Length);

            _fileStream.Seek(0, SeekOrigin.Begin);
            _fileStream.CopyTo(memoryStream);
            _fileStream.Dispose();

            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }

        public void Dispose()
        {
            if (_fileStream.CanWrite)
            {
                // For a disposed FileStream, CanWrite == false
                _fitEncoder.Close();
                _fileStream.Dispose();
                _lock.Dispose();
            }
        }
    }
}
