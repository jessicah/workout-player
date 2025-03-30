using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace BluetoothLE.Models.ContentLibrary
{
    public record Alert(
        [property: JsonPropertyName("icon")] object Icon,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("severity")] string Severity
    );

    public record Description(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("alerts")] IReadOnlyList<Alert> Alerts
    );

    public record Equipment(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("icon")] string Icon
    );

    public record Item(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] string Value
    );

    public record List(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("viewLimit")] int? ViewLimit,
        [property: JsonPropertyName("items")] IReadOnlyList<Item> Items
    );

    public record Metrics(
        [property: JsonPropertyName("ratings")] Ratings Ratings,
        [property: JsonPropertyName("tss")] int? Tss,
        [property: JsonPropertyName("intensityFactor")] double? IntensityFactor
    );

    public record Ratings(
        [property: JsonPropertyName("ac")] int? Ac,
        [property: JsonPropertyName("nm")] int? Nm,
        [property: JsonPropertyName("map")] int? Map,
        [property: JsonPropertyName("ftp")] int? Ftp
    );

    public record Content(
        [property: JsonPropertyName("id")][property: BsonId] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("mediaType")] string MediaType,
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("workoutType")] string WorkoutType,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("level")] string Level,
        [property: JsonPropertyName("order")] int? Order,
        [property: JsonPropertyName("duration")] int? Duration,
        [property: JsonPropertyName("lists")] IReadOnlyList<List> Lists,
        [property: JsonPropertyName("equipment")] IReadOnlyList<Equipment> Equipment,
        [property: JsonPropertyName("metrics")] Metrics Metrics,
        [property: JsonPropertyName("workoutId")] string WorkoutId,
        [property: JsonPropertyName("videoId")] string VideoId,
        [property: JsonPropertyName("descriptions")] IReadOnlyList<Description> Descriptions,
        [property: JsonPropertyName("posterImage")] string PosterImage,
        [property: JsonPropertyName("defaultImage")] string DefaultImage,
        [property: JsonPropertyName("intervalDuration")] string IntervalDuration,
        [property: JsonPropertyName("intensity")] string Intensity,
        [property: JsonPropertyName("roles")] IReadOnlyList<string> Roles,
        [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
        [property: JsonPropertyName("fitnessTest")] bool? FitnessTest,
        [property: JsonPropertyName("planGroupId")] int? PlanGroupId,
        IReadOnlyList<WorkoutTrigger> Workout,
        IReadOnlyList<Timeline> Storylines
    );

    public record class Object<T>(
            [property: JsonPropertyName("position")] int Position,
            [property: JsonPropertyName("positionType")] string PositionType,
            [property: JsonPropertyName("size")] int Size,
            [property: JsonPropertyName("sizeType")] string SizeType,
            [property: JsonPropertyName("type")] string Type,
            [property: JsonPropertyName("parameters")] T Parameters
        )
    {
        public override string ToString()
        {
            return $"Parameters: {Parameters}\nPosition: {Position}; PositionType: {PositionType}; Size: {Size}; SizeType: {SizeType}";
        }
    }

    public record RiderProfile(int Nm, int Ac, int Map, int Ftp);

    public record TypedValue<T>(string Type, T Value);

    public record Storyline(
        [property: BsonId][property: BsonElement("Id")] string VideoId,
        IReadOnlyList<Timeline> Timelines
        );

    public record Timeline(
            [property: JsonPropertyName("position")] int Position,
            [property: JsonPropertyName("positionType")] string PositionType,
            [property: JsonPropertyName("size")] int Size,
            [property: JsonPropertyName("sizeType")] string SizeType,
            [property: JsonPropertyName("type")] string Type,
            [property: JsonPropertyName("objects")] IReadOnlyList<Object<StorylineParameters>> Objects,
            [property: JsonPropertyName("offset")] float Offset
        );

    // Storyline
    public record StorylineParameters(
        [property: JsonPropertyName("textColor")]
        IReadOnlyList<double> TextColor,

        [property: JsonPropertyName("textBackgroundColor")]
        IReadOnlyList<double> TextBackgroundColor,

        [property: JsonPropertyName("subTextColor")]
        IReadOnlyList<double> SubTextColor,

        [property: JsonPropertyName("subTextBackgroundColor")]
        IReadOnlyList<double> SubTextBackgroundColor,

        [property: JsonPropertyName("text")] Text Text,
        [property: JsonPropertyName("subText")] SubText SubText,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] int? Size,
        [property: JsonPropertyName("sizeType")] string SizeType
    );

    public record SubText(
        [property: JsonPropertyName("en")] string En
    );

    public record Text(
        [property: JsonPropertyName("en")] string En
    ); 
    
    public record WorkoutParameters(
 TypedValue<double> Nm,
 TypedValue<double> Ac,
 TypedValue<double> Map,
 TypedValue<double> TwentyMin,
 TypedValue<double> Ftp,
 TypedValue<int> Rpm,
 TypedValue<int> HrZone,
 string Key,
 string Name,
 string IntervalType
    )
    {
        public double? Watts(RiderProfile profile)
        {
            if (Nm != null) return Nm.Value * profile.Nm;
            if (Ac != null) return Ac.Value * profile.Ac;
            if (Map != null) return Map.Value * profile.Map;
            if (TwentyMin != null) return TwentyMin.Value * profile.Ftp;
            if (Ftp != null) return Ftp.Value * profile.Ftp;
            return null;
        }

        public override string ToString()
        {
            return $"NM: {Nm?.Value ?? 0:F2}; AC: {Ac?.Value ?? 0:F2}; MAP: {Map?.Value ?? 0:F2}; FTP: {Ftp?.Value ?? 0:F2}; RPM: {Rpm?.Value ?? 0:F2}";
        }
    }

    public record WorkoutTrigger(
 string Name,
 IReadOnlyList<Track> Tracks
    );

    public record Track(
 IReadOnlyList<Object<WorkoutParameters>> Objects,
 WorkoutParameters Parameters,
 int Size,
 string SizeType,
 string Type
    );

    public record Workout(
        [property: BsonId][property: BsonElement("Id")] string WorkoutId,
        IReadOnlyList<WorkoutTrigger> Triggers
        );
}
