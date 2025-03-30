What is needed timing wise??

videoHandler.Story(currentPosition)

input#seek-bar:
	- @bind=SeekBarPosition
	- @bind:after=ChangePositionAsync

SVGs need to know widths, that are based on total time of workout...

Workout Metrics:
	- Elapsed Time
	- Track Remaining Time (current lap)
	- Workout Remaining Time (total time - current position)

	ElapsedTime >= Timer Time, lap + session

Timestamps can be non-contiguous in FIT Record data

ChangePositionAsync:
	- video.SetCurrentTimeAsync(SeekBarPosition)
	- workoutHandler.ResetTimelines

MAJOR WORK:

WaitForNextTickAsync():
	- gets video time (but could use stopwatch here instead)
	- NoVid: videotime = utcnow - starttime
	- current position = videotime as milliseconds
	- seek bar position = videotime as seconds
	- current targets = workouthandler.targets(current position)
	- upcoming targets = workouthandler.nexttargets(current position)
	- effort change = workouthandler.effortchangeobject(current position)
	- whip = videohandler.whip(current position)

	- current track = get current track(current position)

	- update HR chart
	- update power chart
	- update cadnce chart

	if current track != last track:
		- previous lap: set end time
		- add previous lap to fit file with duration == last track.size ... hmmm
		- add new lap to list of laps with start at current time, and current
		track.size
		- set last track to current track
	- last lap add record

	if index == 0 (i.e. no records added to fit file):
		- fit file handler add start time
		- fit file handler add first record
	
	if index > lastlength (i.e. 1 second has passed):
		- lastlength = index
		- last update... can't remember what this is for..
		- fit file handler add record
		- should we add records for gaps in time?

private class TimingState
{
    public System.Diagnostics.Stopwatch ElapsedTime = new();
    public System.DateTime? StartTime = null;
    public System.DateTime? EndTime = null;
    public System.DateTime? ResumptionTime = null;

    public void Start()
    {
        ElapsedTime.Start();

        ResumptionTime = System.DateTime.UtcNow;

        if (StartTime.HasValue is false)
        {
            StartTime = System.DateTime.UtcNow;
        }
    }

    public void Stop()
    {
        ElapsedTime.Stop();

        EndTime = System.DateTime.UtcNow;
    }

    public TimeSpan TimeOffset { get => (StartTime.HasValue && ResumptionTime.HasValue) ? (ResumptionTime.Value - StartTime.Value) : TimeSpan.Zero; }

    public TimeSpan TotalTimerTime { get => ElapsedTime.Elapsed; }

    public TimeSpan TotalElapsedTime
    {
        get => (StartTime.HasValue, EndTime.HasValue) switch
        {
            (true, true) => EndTime!.Value - StartTime!.Value,
            (true, false) => System.DateTime.UtcNow - StartTime!.Value,
            _ => TimeSpan.Zero
        };
    }
}
