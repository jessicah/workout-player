using BluetoothLE.Models.ContentLibrary;
using System.Collections;

namespace BluetoothLE
{
    public class WorkoutHandler
    {
        Dictionary<string, Timeline> timelines = [];

        Queue<TimeSpan> trackStartTimes = [];

        private readonly int duration = 0;

        public class Timeline
        {
            public LinkedList<Object<WorkoutParameters>> List { get; set; } = [];
            public IEnumerator<Object<WorkoutParameters>> Iterator
            {
                get
                {
                    if (_iterator is null)
                    {
                        _iterator = List.GetEnumerator();
                    }

                    return _iterator;
                }
            }

            public Object<WorkoutParameters>? AtPosition(int position, bool dequeue = false)
            {
                if (List.Count == 0 || _iterator is null) return null;

                do
                {
                    var obj = _iterator.Current;

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

            public void Reset()
            {
                if (List.Count == 0) return;

                _iterator = List.GetEnumerator();
                if (!_iterator.MoveNext())
                {
                    Console.WriteLine($"Unable to move to the start of the enumerator... {_iterator.Current}");
                }

                _lastProcessedPosition = null;
            }

            private IEnumerator<Object<WorkoutParameters>> _iterator = null!;
            private int? _lastProcessedPosition = null;
        }

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

            var obj = timeline.AtPosition(position, dequeue);

            return obj?.Parameters;

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
}
