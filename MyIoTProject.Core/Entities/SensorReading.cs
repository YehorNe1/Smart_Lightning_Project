using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MyIoTProject.Core.Entities
{
    public class SensorReading
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string? Light { get; set; }
        public string? Sound { get; set; }
        public string? Motion { get; set; }

        // Mongo will store as UTC datetime
        public DateTime Timestamp { get; set; }
    }
}