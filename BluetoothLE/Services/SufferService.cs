using BluetoothLE.Models.ContentLibrary;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Immutable;
using static System.ArgumentException;

namespace BluetoothLE.Services
{
    public class SufferService
    {
        public readonly IMongoCollection<Content> ContentCollection = null!;

        public SufferService(IConfiguration config)
        {
            ThrowIfNullOrEmpty(config["MongoDB:Hostname"], "MongoDB:Hostname");
            ThrowIfNullOrEmpty(config["MongoDB:Username"], "MongoDB:Username");
            ThrowIfNullOrEmpty(config["MongoDB:Password"], "MongoDB:Password");

            var client = new MongoClient($"mongodb://{config["MongoDB:Username"]}:{config["MongoDB:Password"]}@{config["MongoDB:Hostname"]}:27017");

            var database = client.GetDatabase("SufferStore");

            ContentCollection = database.GetCollection<Models.ContentLibrary.Content>("Contents");        
        }

        public async Task<List<Content>> GetContentsAsync() => await ContentCollection.Find(_ => true).ToListAsync();

        public async Task<List<Content>> GetContentsByChannelAsync(string channel, string workoutType)
        {
            var filterBuilder = Builders<Content>.Filter;
            var filter = filterBuilder.Eq(item => item.Channel, channel)
                & filterBuilder.Eq(item => item.WorkoutType, workoutType);

            var sortBuilder = Builders<Content>.Sort;
            var sorter = sortBuilder.Ascending(item => item.Name);

            return await ContentCollection.Find(filter).Sort(sorter).ToListAsync();
        }

        public async Task<IAsyncCursor<Content>> GetContentsByChannelAsyncCursor(string channel, string workoutType)
        {
            var filterBuilder = Builders<Content>.Filter;
            var filter = filterBuilder.Eq(item => item.Channel, channel)
                & filterBuilder.Eq(item => item.WorkoutType, workoutType);

            var sorter = Builders<Content>.Sort.Ascending(item => item.Name);

            return await ContentCollection.Find(filter).Sort(sorter).ToCursorAsync();
        }

        public record LibraryItem(string Id, string Name, string Channel, string WorkoutType, IReadOnlyList<WorkoutTrigger> Workout, bool HasStorylines, Metrics Metrics);

        public async Task<List<LibraryItem>> GetLibrary()
        {
            var projection = Builders<Content>.Projection.Expression(content => new LibraryItem
            (
                content.Id,
                content.Name,
                content.Channel,
                content.WorkoutType,
                content.Workout,
                content.Storylines.Any(),
                content.Metrics
            ));

            var sort = Builders<Content>.Sort.Ascending(item => item.Name);

            return await ContentCollection.Find(_ => true).Project(projection).Sort(sort).ToListAsync();
        }

        public record LibraryCategory(string Channel, string WorkoutType);

        public async Task<List<IGrouping<LibraryCategory, LibraryItem>>> GetLibraryAsync()
        {
            var projection = Builders<Content>.Projection.Expression(content => new LibraryItem
            (
                content.Id,
                content.Name,
                content.Channel,
                content.WorkoutType,
                content.Workout,
                content.Storylines.Any(),
                content.Metrics
            ));

            var sort = Builders<Content>.Sort.Ascending(item => item.Name);

            var pipeline = new EmptyPipelineDefinition<Content>()
                .Sort(sort)
                .Project(projection)
                .Group(item => new LibraryCategory(item.Channel, item.WorkoutType), items => items);

            return await ContentCollection.Aggregate(pipeline).ToListAsync();
        }
    }
}
