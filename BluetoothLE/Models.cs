using System.Text.Json;
using System.Text.Json.Serialization;

namespace BluetoothLE.Models
{
    // Root myDeserializedClass = JsonSerializer.Deserialize<List<Root>>(myJsonResponse);
    public record Object<T>(
        [property: JsonPropertyName("position")] int Position,
        [property: JsonPropertyName("positionType")] string PositionType,
        [property: JsonPropertyName("size")] int Size,
        [property: JsonPropertyName("sizeType")] string SizeType,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("parameters")] T Parameters
    );

    // Storyline
    public record StorylineParameters(
        [property: JsonPropertyName("textColor")] IReadOnlyList<double> TextColor,
        [property: JsonPropertyName("textBackgroundColor")] IReadOnlyList<double> TextBackgroundColor,
        [property: JsonPropertyName("subTextColor")] IReadOnlyList<double> SubTextColor,
        [property: JsonPropertyName("subTextBackgroundColor")] IReadOnlyList<double> SubTextBackgroundColor,
        [property: JsonPropertyName("text")] Text Text,
        [property: JsonPropertyName("subText")] SubText SubText,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] int? Size,
        [property: JsonPropertyName("sizeType")] string SizeType
    )
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    public record WorkoutParameters(
        [property: JsonPropertyName("ftp")] Target<double> Ftp,
        [property: JsonPropertyName("rpm")] Target<int> Rpm,
        [property: JsonPropertyName("grade")] Target<double> Grade,
        [property: JsonPropertyName("rpe")] Target<double> Rpe,
        [property: JsonPropertyName("hrZone")] Target<int> HrZone,
        [property: JsonPropertyName("twentyMin")] Target<double> TwentyMin,
        [property: JsonPropertyName("map")] Target<double> Map,
        [property: JsonPropertyName("nm")] Target<double> Nm,
        [property: JsonPropertyName("ac")] Target<double> Ac,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("intervalType")] string IntervalType
    )
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    public record Root(
        [property: JsonPropertyName("position")] int Position,
        [property: JsonPropertyName("positionType")] string PositionType,
        [property: JsonPropertyName("size")] int Size,
        [property: JsonPropertyName("sizeType")] string SizeType,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("objects")] IReadOnlyList<Object<StorylineParameters>> Objects,
        [property: JsonPropertyName("offset")] float Offset
    );

    public record SubText(
        [property: JsonPropertyName("en")] string En
    );

    public record Text(
        [property: JsonPropertyName("en")] string En
    );

    public record Target<T>(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] T Value
    );

    public record Track(
        [property: JsonPropertyName("objects")] IReadOnlyList<Object<WorkoutParameters>> Objects,
        [property: JsonPropertyName("parameters")] WorkoutParameters Parameters,
        [property: JsonPropertyName("size")] int Size,
        [property: JsonPropertyName("sizeType")] string SizeType,
        [property: JsonPropertyName("type")] string Type
        //[property: JsonPropertyName("variables")] Variables Variables
    );

    public record Variables(
        [property: JsonPropertyName("constants")] IReadOnlyList<object> Constants,
        [property: JsonPropertyName("aggregates")] IReadOnlyList<object> Aggregates,
        [property: JsonPropertyName("custom")] IReadOnlyList<object> Custom
    );

    public record WorkoutSection(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("tracks")] IReadOnlyList<Track> Tracks
        //[property: JsonPropertyName("variables")] Variables Variables,
        //[property: JsonPropertyName("testResults")] object TestResults,
        //[property: JsonPropertyName("userClass")] object UserClass,
        //[property: JsonPropertyName("analysis")] object Analysis
    );
}
