using System.Threading.Tasks;
using MyIoTProject.Core.Entities;

namespace MyIoTProject.Core.Interfaces
{
    public interface ISensorReadingRepository
    {
        Task InsertReadingAsync(SensorReading reading);
    }
}