using BluetoothLE.Models.ContentLibrary;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BluetoothLE
{
    public class VideoHandler
    {
        public VideoHandler(Timeline root)
        {
            foreach (var item in root.Objects)
            {
                if (timelines.TryGetValue(item.Type, out var timeline) == false)
                {
                    timeline = new();

                    timelines.Add(item.Type, timeline);
                }

                timeline.Enqueue(item);

                if (item.Type != StringKeys.Story)
                {
                    Console.WriteLine($"{item.Type}:");
                    Console.WriteLine($"  Parameters:");
                    if (item.Parameters.Text is Text text)
                    {
                        Console.WriteLine($"    Text: {text.En}");
                    }
                    if (item.Parameters.SubText is SubText subText)
                    {
                        Console.WriteLine($"    SubText: {subText.En}");
                    }
                    if (item.Parameters.Key is string key && key.Length > 0)
                    {
                        Console.WriteLine($"    Key: {key}");
                    }

                    Console.WriteLine($"  Starts at: {TimeSpan.FromMilliseconds(item.Position)}");
                }
            }

            duration = root.Size;
            // Really don't know what this is used for...
            //offset = (int)(root.Offset * 1000);
        }

        Dictionary<string, Queue<Object<StorylineParameters>>> timelines = [];

        private readonly int duration = 0;
        private readonly int offset = 0;

        private StorylineParameters? CurrentOfType(int position, string type, bool dequeue=false)
        {
            //Console.WriteLine($"Current at {position}");

            if (timelines.TryGetValue(type, out var timeline) == false)
            {
                //Console.WriteLine($"Unable to locate video timeline of type {type}");
            }

            if (timeline is null || position >= duration || timeline.Count == 0)
                return null;

            while (timeline.FirstOrDefault() is Object<StorylineParameters> obj)
            {
                // TODO: to support multiple object types, it is probably better to return a list,
                // and return all objects in the range... tricky though... which goes back to the
                // earlier TODO of using an array instead of a queue...
                if (obj.Type != type)
                {
                    timeline.Dequeue();

                    Console.WriteLine($"Non-{type} object: {obj.Type}:");
                    Console.WriteLine($"  Parameters:");
                    if (obj.Parameters.Text is Text text)
                    {
                        Console.WriteLine($"    Text: {text.En}");
                    }
                    if (obj.Parameters.SubText is SubText subText)
                    {
                        Console.WriteLine($"    SubText: {subText.En}");
                    }
                    //if (obj.Parameters.ExtensionData is IDictionary<string, JsonElement> extraData)
                    //{
                    //    foreach (var (k, v) in extraData)
                    //    {
                    //        Console.WriteLine($"    {k}: {v}");
                    //    }
                    //}
                }
                else if (position > obj.Position + obj.Size + offset)
                {
                    // this object has already expired...
                    timeline.Dequeue();
                }
                else if (position >= obj.Position + offset && position < obj.Position + obj.Size + offset)
                {
                    // else the object is active
                    if (dequeue)
                        timeline.Dequeue();

                    return obj.Parameters;
                }
                else
                {
                    // else the next object is in the future
                    return null;
                }
            }

            return null;
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
    }
}
