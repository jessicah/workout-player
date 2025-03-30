using System.Diagnostics;

namespace FixTimers
{
    internal class Program
    {
        private static Dynastream.Fit.Encode fitEncoder = new(Dynastream.Fit.ProtocolVersion.V10);

        private static uint startTime = uint.MaxValue;

        private static DateTime? endDt = null;

        static void Main(string[] args)
        {
            //var path = args[1];
            var path = @"G:\testing\2025-03-26-00-03 - Cadence Builds - Copy.fit";

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read);

            var fixedPath = $"{path}.fixed.fit";

            var outputStream = new FileStream(fixedPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            outputStream.SetLength(0);

            fitEncoder.Open(outputStream);

            var fitDecoder = new Dynastream.Fit.Decode();

            fitDecoder.MesgEvent += FitDecoder_MesgEvent;

            fitDecoder.Read(stream);

            fitEncoder.Close();
            outputStream.Flush();
            outputStream.Close();

            Console.WriteLine($"Processed {numRecords} records");
        }

        private static int numRecords = 0;

        private static uint lastRecordTimestamp = 0;

        private static void FitDecoder_MesgEvent(object sender, Dynastream.Fit.MesgEventArgs e)
        {
            if (e.mesg.Num == Dynastream.Fit.MesgNum.Lap)
            {
                if (e.mesg.GetField(Dynastream.Fit.LapMesg.FieldDefNum.StartTime).GetValue() is uint lapStartTime && lapStartTime < startTime)
                {
                    startTime = lapStartTime;

                    Console.WriteLine($"Updated true start time to {startTime}");
                }
            }

            if (e.mesg.Num == Dynastream.Fit.MesgNum.FieldDescription)
                return;

            if (e.mesg.Num == Dynastream.Fit.MesgNum.Record)
            {
                ++numRecords;
                if (e.mesg.GetField(Dynastream.Fit.RecordMesg.FieldDefNum.Timestamp).GetValue() is uint recordTimestamp)
                {
                    if (recordTimestamp < lastRecordTimestamp)
                    {
                        Console.WriteLine($"Need to update record timestamp...");
                    }
                    else
                    {
                        lastRecordTimestamp = recordTimestamp;
                        Console.WriteLine($"Timestamp: {lastRecordTimestamp}");
                    }
                }
            }

            if (e.mesg.Num == Dynastream.Fit.MesgNum.Session)
            {
                Console.WriteLine("Session to fix");

                var dt = new Dynastream.Fit.DateTime(startTime);

                var endTime = e.mesg.GetField(Dynastream.Fit.SessionMesg.FieldDefNum.Timestamp);

                endDt = new Dynastream.Fit.DateTime((uint)endTime.GetValue()).GetDateTime();

                var sessionMessage = new Dynastream.Fit.SessionMesg(e.mesg);
                var totalSeconds = (float)(endDt.Value - dt.GetDateTime()).TotalSeconds;
                sessionMessage.StartTime = dt;
                sessionMessage.TotalElapsedTime = totalSeconds;
                sessionMessage.TotalTimerTime = totalSeconds;

                fitEncoder.Write(sessionMessage);
            }
            else if (e.mesg.Num == Dynastream.Fit.MesgNum.Activity)
            {
                Console.WriteLine("Activity to fix");

                var dt = new Dynastream.Fit.DateTime(startTime).GetDateTime();

                var activityMessage = new Dynastream.Fit.ActivityMesg(e.mesg);
                var totalSeconds = (float)(endDt.Value - dt).TotalSeconds;
                activityMessage.TotalTimerTime = totalSeconds;

                fitEncoder.Write(activityMessage);
            }
            else
            {
                fitEncoder.Write(e.mesg);
            }
        }
    }
}
