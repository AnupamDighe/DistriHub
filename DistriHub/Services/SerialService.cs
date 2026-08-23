using System.Threading.Tasks;
using DistriHub.Repository;
using DistriHub.Services.Interfaces;

namespace DistriHub.Services
{
    public class SerialService : ISerialService
    {
        private readonly IRepository _repo;

        public SerialService(IRepository repo)
        {
            _repo = repo;
        }

        public Task<int> ValidateSerialAsync(string materialCode, string serialNumber, string source, string accessCode)
        {
            return _repo.ValidateSerialAsync(materialCode, serialNumber, source, accessCode);
        }

        public Task<int> UnfreezeSerialAsync(string materialCode, string serialNumber, string source, string accessCode)
        {
            return _repo.UnfreezeSerialAsync(materialCode, serialNumber, source, accessCode);
        }
    }
}
