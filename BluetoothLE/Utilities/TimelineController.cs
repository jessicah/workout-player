using Microsoft.EntityFrameworkCore.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace BluetoothLE.Utilities
{
    /*
     * This has one single source of truth for elapsed time, instead of
     * several, but does it produce the values we're after?
     */
    public sealed class TimingTest
    {
        public Stopwatch ElapsedTimer = new();
        public TimeSpan TotalPausedTime = TimeSpan.Zero;
        public TimeSpan LapStartedAt = TimeSpan.Zero;
        public TimeSpan TotalLapPausedTime = TimeSpan.Zero;

        public DateTime? StartedAt = null;
        public DateTime? LastPausedAt = null;

        public TimeSpan Elapsed = TimeSpan.Zero;

        [MemberNotNull(nameof(StartedAt))]
        public void Start()
        {
            ElapsedTimer.Start();

            StartedAt = DateTime.UtcNow;
        }

        [MemberNotNull(nameof(LastPausedAt))]
        public void Pause()
        {
            LastPausedAt = DateTime.UtcNow;
        }

        public void Resume()
        {
            if (LastPausedAt.HasValue is false)
                return;

            DateTime now = DateTime.UtcNow;

            TotalLapPausedTime += now - LastPausedAt.Value;
            TotalPausedTime += now - LastPausedAt.Value;
        }

        public void Mark()
        {
            if (StartedAt.HasValue is false)
                return;

            Elapsed = ElapsedTimer.Elapsed;

            LapStartedAt = DateTime.UtcNow - StartedAt.Value;
        }
        
        public void Stop()
        {
            ElapsedTimer.Stop();

            Elapsed = ElapsedTimer.Elapsed;
        }

        public TimeSpan TotalTime { get => Elapsed - TotalPausedTime; }
        public TimeSpan ElapsedTime { get => Elapsed; }
        public TimeSpan LapTotalTime { get => Elapsed - LapStartedAt - TotalLapPausedTime; }
        public TimeSpan LapElapsedTime { get => Elapsed - LapStartedAt; }
    }

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

        // Need a base timeline, which maps a video/workout to
        // Or do we? If it's just an open range, where is the utility...?
        public int[] BaseTimeline { get; set; } = [];

        public Range BaseRange { get; set; } = ..;

        // This is made up of chunks of the base timeline...
        public List<Range> TimelineChunks { get; } = [];

        public Range CurrentChunk { get; private set; } = ..;
        public DateTime ChunkStartedAt { get; private set; } = DateTime.UtcNow;

        public int StartIndex { get; private set; } = 0;
        public int CurrentIndex { get; private set; } = 0;
        // `UpdateIndex` takes a value in milliseconds as an absolute value for the
        // workout timeline; but notify events only happen on changes >= 1s, so this
        // is purely read elsewhere where greater precision is needed...
        public int CurrentMilliseconds { get => (int)(AbsoluteSeconds * 1000); }

        private double AbsoluteSeconds { get; set; } = 0.0;

        // Elapsed timer is wall clock time, so when paused, this continues ticking
        public Stopwatch ElapsedTimer = new();

        // Total timer is total _active_ time, so stops during pauses
        public Stopwatch TotalTimer = new();

        public DateTime? StartedAt { get; private set; } = null;
        public DateTime? EndedAt { get; private set; } = null;

        // Should we store lap data here too?
        public DateTime? LapStartedAt { get; private set; } = null;
        public Stopwatch LapElapsedTimer = new();
        public Stopwatch LapTotalTimer = new();

        public event Action<int, int, TimerState>? OnIndexChanged;

        private void NotifyIndexChanged(int previousIndex, int currentIndex, TimerState timerState) => OnIndexChanged?.Invoke(previousIndex, currentIndex, timerState);

        public TimelineController()
        {
            // Do we want to take a size to set the base timeline range?
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

            StartIndex = CurrentIndex = newIndex;
            AbsoluteSeconds = newIndex;

            NotifyIndexChanged(previousIndex, CurrentIndex, ElapsedTimer.IsRunning ? TimerState.Playing : TimerState.Paused);

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
            // Using open ended range, so don't need to update it...
            //CurrentChunk = CurrentChunk.Start..CurrentIndex;
        }

        [MemberNotNull(nameof(StartedAt))]
        [MemberNotNull(nameof(LapStartedAt))]
        public void Start()
        {
            if (StartedAt.HasValue is false)
            {
                LapStartedAt = StartedAt = DateTime.UtcNow;
                LapElapsedTimer.Start();
                LapTotalTimer.Start();
            }

            ElapsedTimer.Start();
            TotalTimer.Start();
        }

        public void Pause()
        {
            LapTotalTimer.Stop();
            TotalTimer.Stop();
        }

        [MemberNotNull(nameof(EndedAt))]
        public void Stop()
        {
            if (EndedAt.HasValue)
                return;

            EndedAt = DateTime.UtcNow;

            ElapsedTimer.Stop();
            TotalTimer.Stop();

            LapElapsedTimer.Stop();
            LapTotalTimer.Stop();

            // Update to the final closed interval
            CurrentChunk = CurrentChunk.Start..CurrentIndex;
            TimelineChunks.Add(CurrentChunk);

            Console.WriteLine("Final Chunks:");
            foreach (var chunk in TimelineChunks)
            {
                Console.WriteLine($"    {chunk.Start} => {chunk.End}");
            }
        }

        [MemberNotNull(nameof(LapStartedAt))]
        public void Mark()
        {
            LapStartedAt = DateTime.UtcNow;
            LapElapsedTimer.Restart();
            LapTotalTimer.Restart();
        }

        public TimeSpan TotalTime { get => TotalTimer.Elapsed; }
        public TimeSpan ElapsedTime { get => ElapsedTimer.Elapsed; }
        public TimeSpan LapTotalTime { get => LapTotalTimer.Elapsed; }
        public TimeSpan LapElapsedTime { get => LapElapsedTimer.Elapsed; }
    }
}
