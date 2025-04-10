using System.Threading.Tasks;
using MongoDB.Driver;
using MyIoTProject.Core.Entities;
using MyIoTProject.Core.Interfaces;

namespace MyIoTProject.Infrastructure.Repositories
{
    public class SensorReadingRepository : ISensorReadingRepository
    {
        private readonly IMongoCollection<SensorReading> _collection;

        public SensorReadingRepository(string mongoConnectionString, string databaseName, string collectionName)
        {
            var client = new MongoClient(mongoConnectionString);
            var database = client.GetDatabase(databaseName);
            _collection = database.GetCollection<SensorReading>(collectionName);
        }

        public async Task InsertReadingAsync(SensorReading reading)
        {
            await _collection.InsertOneAsync(reading);
        }
    }
}