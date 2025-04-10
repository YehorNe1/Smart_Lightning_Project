using System;
using System.Threading.Tasks;
using MyIoTProject.Core.Entities;
using MyIoTProject.Core.Interfaces;

namespace MyIoTProject.Application.Services
{
    public class SensorReadingService
    {
        private readonly ISensorReadingRepository _repository;

        public SensorReadingService(ISensorReadingRepository repository)
        {
            _repository = repository;
        }

        public async Task AddReadingAsync(string light, string sound, string motion)
        {
            // Filter logic: only store if conditions are met
            bool shouldStore = false;

            // Example: store if motion detected, or sound is quite/very loud, or light is very bright
            if (motion == "Motion detected!") shouldStore = true;
            if (sound == "Quite loud" || sound == "Very loud!") shouldStore = true;
            if (light == "Very bright") shouldStore = true;

            if (!shouldStore)
            {
                Console.WriteLine("Data does not meet the filter criteria. Skipping DB insertion.");
                return;
            }

            var reading = new SensorReading
            {
                Light = light,
                Sound = sound,
                Motion = motion,
                Timestamp = DateTime.UtcNow
            };

            await _repository.InsertReadingAsync(reading);
            Console.WriteLine("Filtered reading inserted into DB.");
        }
    }
}