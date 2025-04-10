using System;

namespace MyIoTProject.Core.Entities
{
    public class SensorReading
    {
        public string? Id { get; set; }
        public string? Light { get; set; }
        public string? Sound { get; set; }
        public string? Motion { get; set; }
        public DateTime Timestamp { get; set; }
    }
}