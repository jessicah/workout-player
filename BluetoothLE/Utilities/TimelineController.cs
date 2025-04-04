using Microsoft.EntityFrameworkCore.Design;
using NuGet.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace BluetoothLE.Utilities
{
    /*
     * This is responsible for handling the workout timelines...
     * 
     * Allows for cutting the base timeline to allow for seeking,
     * stores all the various indices for workout UI, timestamps
     * for FIT files, etc.
     */
    public sealed class TimelineController
    {
        public enum TimerState { Paused, Playing }

        // This is made up of chunks of the base timeline...
        public List<Range> TimelineChunks { get; } = [];

        public Range CurrentChunk { get; private set; } = ..;

        public int CurrentIndex { get; private set; } = 0;

        // `UpdateIndex` takes a value in milliseconds as an absolute value for the
        // workout timeline; but notify events only happen on changes >= 1s, so this
        // is purely read elsewhere where greater precision is needed...
        public int CurrentMilliseconds { get => (int)(AbsoluteSeconds * 1000); }

        private double AbsoluteSeconds { get; set; } = 0.0;

        // Elapsed timer is wall clock time, so when paused, this continues ticking
        public Stopwatch ElapsedTimer = new();

        public DateTime? StartedAt { get; private set; } = null;
        public DateTime? EndedAt { get; private set; } = null;
        public DateTime? LapStartedAt { get; private set; } = null;
        public DateTime? LastPausedAt { get; private set; } = null;

        public TimeSpan TotalPausedTime { get; private set; } = TimeSpan.Zero;
        public TimeSpan TotalLapPausedTime { get; private set; } = TimeSpan.Zero;

        public bool IsPaused { get; private set; } = true;

        public event Action<int, int, TimerState>? OnIndexChanged;

        private void NotifyIndexChanged(int previousIndex, int currentIndex, TimerState timerState) => OnIndexChanged?.Invoke(previousIndex, currentIndex, timerState);

        private DateTime _lastTickAt = DateTime.UtcNow;

        public TimelineController()
        {
        }

        public void Reposition(int newIndex)
        {
            if (newIndex == CurrentIndex)
                return;

            int previousIndex = CurrentIndex;

            // Update the chunk to a closed interval
            CurrentChunk = CurrentChunk.Start..CurrentIndex;

            TimelineChunks.Add(CurrentChunk);

            // Start a new chunk, open ended
            CurrentChunk = newIndex..;

            AbsoluteSeconds = newIndex;

            NotifyIndexChanged(previousIndex, CurrentIndex, ElapsedTimer.IsRunning ? TimerState.Playing : TimerState.Paused);

            CurrentIndex = newIndex;

            Console.WriteLine("Chunks:");
            foreach (var chunk in TimelineChunks)
            {
                Console.WriteLine($"    {chunk.Start} => {chunk.End}");
            }
        }

        public void UpdateIndex(double absoluteSeconds)
        {
            AbsoluteSeconds = absoluteSeconds;

            int newPosition = (int)absoluteSeconds;

            if (CurrentIndex == newPosition)
                return;

            // maybe we have some event handlers that need to do something on position change?
            // then we don't need to know what they are, or to store them in here...
            NotifyIndexChanged(CurrentIndex, newPosition, ElapsedTimer.IsRunning ? TimerState.Playing : TimerState.Paused);

            CurrentIndex = newPosition;
        }

        public void Tick()
        {
            if (IsPaused)
                return;

            DateTime now = DateTime.UtcNow;

            TimeSpan tickSize = now - _lastTickAt;

            _lastTickAt = now;

            UpdateIndex(AbsoluteSeconds + tickSize.TotalSeconds);
        }

        [MemberNotNull(nameof(StartedAt))]
        [MemberNotNull(nameof(LapStartedAt))]
        public void Start()
        {
            if (StartedAt.HasValue)
            {
                Resume();

                return;
            }

            ElapsedTimer.Start();

            LapStartedAt = StartedAt = DateTime.UtcNow;

            _lastTickAt = StartedAt.Value;

            IsPaused = false;

            Console.WriteLine($"Start():");
            Console.WriteLine($"  Started At: {StartedAt}");
        }

        public void Resume()
        {
            if (LastPausedAt.HasValue is false)
            {
                Console.WriteLine("WARNING: TimelineController::Resume(); don't know when we were paused, can't 'resume'.");

                return;
            }

            var now = DateTime.UtcNow;

            TotalLapPausedTime += now - LastPausedAt.Value;
            TotalPausedTime += now - LastPausedAt.Value;

            LastPausedAt = null;

            _lastTickAt = now;

            IsPaused = false;

            Console.WriteLine($"Resume():");
            Console.WriteLine($"  Total Lap Paused Time: {TotalLapPausedTime}");
            Console.WriteLine($"  Total Paused Time: {TotalPausedTime}");
        }

        [MemberNotNull(nameof(LastPausedAt))]
        public void Pause()
        {
            LastPausedAt = DateTime.UtcNow;

            IsPaused = true;

            Console.WriteLine($"Pause():");
            Console.WriteLine($"  Last Paused At: {LastPausedAt}");
        }

        public (TimeSpan LapElapsedTime, TimeSpan LapTotalTime) MarkLap()
        {
            if (LapStartedAt.HasValue is false)
            {
                Console.WriteLine("WARNING: TimelineController::MarkLap(); don't know when lap started, can't 'mark' lap.");

                return (TimeSpan.Zero, TimeSpan.Zero);
            }

            var now = DateTime.UtcNow;

            var lapElapsedTime = now - LapStartedAt.Value;
            var lapTotalTime = now - LapStartedAt.Value - TotalLapPausedTime;

            LapStartedAt = now;
            TotalLapPausedTime = TimeSpan.Zero;

            Console.WriteLine($"Mark():");
            Console.WriteLine($"  Lap Elapsed: {lapElapsedTime}");
            Console.WriteLine($"  Next Lap Started At: {LapStartedAt}");

            return (lapElapsedTime, lapTotalTime);
        }

        [MemberNotNull(nameof(StartedAt))]
        [MemberNotNull(nameof(LapStartedAt))]
        [MemberNotNull(nameof(EndedAt))]
        public (TimeSpan LapElapsedTime, TimeSpan LapTotalTime, TimeSpan ElapsedTime, TimeSpan TotalTime) Stop()
        {
            if (StartedAt.HasValue is false)
            {
                LapStartedAt = StartedAt = DateTime.UtcNow;
            }
            if (LapStartedAt.HasValue is false)
            {
                LapStartedAt = StartedAt;
            }

            if (EndedAt.HasValue)
            {
                goto endedAtResults;
            }

            EndedAt = DateTime.UtcNow;

            ElapsedTimer.Stop();

            // Update to the final closed interval
            CurrentChunk = CurrentChunk.Start..CurrentIndex;
            TimelineChunks.Add(CurrentChunk);

            IsPaused = true;

            Console.WriteLine("Final Chunks:");
            foreach (var chunk in TimelineChunks)
            {
                Console.WriteLine($"    {chunk.Start} => {chunk.End}");
            }

        endedAtResults:
            var lapElapsedTime = EndedAt.Value - LapStartedAt.Value;
            var lapTotalTime = EndedAt.Value - LapStartedAt.Value - TotalLapPausedTime;
            var elapsedTime = ElapsedTimer.Elapsed;
            var totalTime = ElapsedTimer.Elapsed - TotalPausedTime;

            Console.WriteLine($"Stop():");
            Console.WriteLine($"  Elapsed: {elapsedTime}");

            return (lapElapsedTime, lapTotalTime, elapsedTime, totalTime);
        }

        //public TimeSpan TotalTime { get => ElapsedTimer.Elapsed - TotalPausedTime; }
        public TimeSpan ElapsedTime { get => ElapsedTimer.Elapsed; }

        public TimeSpan TotalTime
        {
            get
            {
                if (StartedAt.HasValue is false)
                    return TimeSpan.Zero;

                if (IsPaused is false || LastPausedAt.HasValue is false)
                {
                    return ElapsedTimer.Elapsed - TotalPausedTime;
                }
                else
                {
                    return LastPausedAt.Value - StartedAt.Value - TotalPausedTime;
                }
            }
        }
    }
}
