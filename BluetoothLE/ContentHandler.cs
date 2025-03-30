using BluetoothLE.Models.ContentLibrary;
using System.Collections;
using static BluetoothLE.WorkoutHandler;

namespace BluetoothLE.ContentHandler
{
    public class Timeline<TParameter>
    {
        public LinkedList<Object<TParameter>> List { get; set; } = [];

        public Object<TParameter>? AtPosition(int position, bool dequeue = false)
        {
            if (List.Count == 0) return null;

            if ((_iterator is null || (_lastProcessedPosition is int && position < _lastProcessedPosition)) && !Reset())
            {
                var it = _iterator is null ? "null" : "nonnull";
                var rewind = position < _lastProcessedPosition ? "true" : "false";

                Console.WriteLine($"Unable to reset: iterator is {it}; rewind is {rewind}");

                return null;
            }

            do
            {
                var obj = _iterator!.Current;

                if (position > obj.Position + obj.Size)
                {
                    // move to the next node...
                    continue;
                }
                else if (position >= obj.Position && position < obj.Position + obj.Size)
                {
                    // within current track item
                    if (dequeue)
                    {
                        if (_lastProcessedPosition == obj.Position)
                        {
                            // already processed, and dequeuing...
                            return null;
                        }
                        else
                        {
                            // first time at this position, return the value
                            _lastProcessedPosition = obj.Position;
                        }
                    }

                    return obj;
                }
                else
                {
                    // next track item is in the future
                    return null;
                }
            } while (_iterator.MoveNext());

            // no more tracks
            return null;
        }

        public bool Reset()
        {
            _iterator = List.GetEnumerator();
            _lastProcessedPosition = null;

            return _iterator.MoveNext();
        }

        private IEnumerator<Object<TParameter>> _iterator = null!;
        private int? _lastProcessedPosition = null;
    }

    public class WorkoutHandler
    {
        Dictionary<string, Timeline<WorkoutParameters>> timelines = [];

        Queue<TimeSpan> trackStartTimes = [];

        private readonly int duration = 0;

        public WorkoutHandler(IReadOnlyList<WorkoutTrigger> sections)
        {
            TimeSpan previousTrackStart = TimeSpan.Zero;

            trackStartTimes.Enqueue(previousTrackStart);

            int changeEndsAt = 0;

            foreach (var section in sections)
            {
                foreach (var track in section.Tracks)
                {
                    TimeSpan nextStart = previousTrackStart.Add(TimeSpan.FromMilliseconds(track.Size));

                    trackStartTimes.Enqueue(nextStart);

                    for (var objectIndex = 0; objectIndex < track.Objects.Count; ++objectIndex)
                    {
                        var item = track.Objects[objectIndex];

                        if (item.Type == StringKeys.SoundEffect && item.Parameters.Key == StringKeys.EffortChangeComing)
                        {
                            if (timelines.TryGetValue(item.Type, out var soundEffectTimeline) == false)
                            {
                                soundEffectTimeline = new();

                                timelines.Add(item.Type, soundEffectTimeline);
                            }

                            soundEffectTimeline.List.AddLast(item with { Position = item.Position + (int)previousTrackStart.TotalMilliseconds });

                            item = item with { Type = StringKeys.EffortChange };

                            var nextTarget = track.Objects.Skip(objectIndex).FirstOrDefault(obj => obj.Type == StringKeys.Targets);

                            if (nextTarget is null)
                            {
                                Console.WriteLine("ERROR: unable to locate the next target for effort change");
                            }
                            else
                            {
                                item = item with { Size = nextTarget.Position - item.Position };
                            }

                            changeEndsAt = item.Position + (int)previousTrackStart.TotalMilliseconds + item.Size;
                        }

                        if (timelines.TryGetValue(item.Type, out var timeline) == false)
                        {
                            timeline = new();

                            timelines.Add(item.Type, timeline);
                        }

                        var absoluteItem = item with { Position = item.Position + (int)previousTrackStart.TotalMilliseconds };

                        timeline.List.AddLast(absoluteItem);
                    }

                    previousTrackStart = nextStart;
                }
            }

            duration = (int)previousTrackStart.TotalMilliseconds;

            // set each timeline ready for queries
            foreach (var (_, timeline) in timelines)
            {
                timeline.Reset();
            }
        }

        public void ResetTimelines()
        {
            foreach (var (_, timeline) in timelines)
            {
                timeline.Reset();
            }
        }

        private WorkoutParameters? CurrentOfType(int position, string type, bool dequeue = false)
        {
            if (timelines.TryGetValue(type, out var timeline) == false)
            {
                Console.WriteLine($"Unable to locate workout timeline of type {type}");
            }

            if (timeline is null || position >= duration)
            {
                return null;
            }

            return timeline.AtPosition(position, dequeue)?.Parameters;
        }

        private Object<WorkoutParameters>? CurrentObjectOfType(int position, string type, bool dequeue = false)
        {
            if (timelines.TryGetValue(type, out var timeline) == false)
            {
                Console.WriteLine($"Unable to locate workout timeline of type {type}");
            }

            if (timeline is null || position >= duration)
            {
                return null;
            }

            return timeline.AtPosition(position, dequeue);
        }

        public WorkoutParameters? Targets(int position) => CurrentOfType(position, StringKeys.Targets);
        public WorkoutParameters? SoundEffect(int position) => CurrentOfType(position, StringKeys.SoundEffect, true);
        public WorkoutParameters? EffortChange(int position) => CurrentOfType(position, StringKeys.EffortChange);

        public Object<WorkoutParameters>? EffortChangeObject(int position) => CurrentObjectOfType(position, StringKeys.EffortChange);

        public WorkoutParameters? NextTargets(int position)
        {
            if (timelines.TryGetValue(StringKeys.Targets, out var timeline) == false)
            {
                return null;
            }

            if (timeline is null || position >= duration)
            {
                return null;
            }

            bool found = false;

            foreach (var target in timeline.List)
            {
                if (found)
                {
                    return target.Parameters;
                }

                if (position >= target.Position && position < target.Position + target.Size)
                {
                    found = true;
                }
            }

            return null;
        }
    }

    public class VideoHandler
    {
        Dictionary<string, Timeline<StorylineParameters>> timelines = [];

        private readonly int duration = 0;
        private readonly int offset = 0;

        public VideoHandler(Models.ContentLibrary.Timeline root)
        {
            foreach (var item in root.Objects)
            {
                if (timelines.TryGetValue(item.Type, out var timeline) == false)
                {
                    timeline = new();

                    timelines.Add(item.Type, timeline);
                }

                timeline.List.AddLast(item);
            }

            duration = root.Size;

            // Really don't know what this is used for...
            offset = (int)(root.Offset * 1000);
        }

        private StorylineParameters? CurrentOfType(int position, string type, bool dequeue = false)
        {
            if (timelines.TryGetValue(type, out var timeline) == false)
            {
                return null;
            }

            if (timeline is null || position >= duration)
                return null;

            return timeline.AtPosition(position, dequeue)?.Parameters;
        }

        public StorylineParameters? Story(int position) => CurrentOfType(position, StringKeys.Story);
        public StorylineParameters? Music(int position) => CurrentOfType(position, StringKeys.Music);
        public StorylineParameters? Footage(int position) => CurrentOfType(position, StringKeys.Footage);
        public StorylineParameters? Bubble(int position) => CurrentOfType(position, StringKeys.Bubble);
        public StorylineParameters? SoundEffect(int position) => CurrentOfType(position, StringKeys.SoundEffect, true);
        public StorylineParameters? Whip(int position) => CurrentOfType(position, StringKeys.Whip);

        // TODO: there are also "music" and "footage" types that could
        // possibly be handled as well...

        // Stumbled upon more: "bubble", "history", "soundEffect", "whip",
        // and even an "unknown"... wat

        // A Very Dark Place has bubble, footage, music, soundEffect, story, and whip;
        // and sound effects are in addition to sound effects in the triggers...
        // because why not? Urgh...

        // But sound effects are in relation to "whip"... can we merge the two? Is it worth
        // the effort to merge them? Probably not...
    }
}
