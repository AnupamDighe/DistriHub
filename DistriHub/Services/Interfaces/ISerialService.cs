using System.Threading.Tasks;

namespace DistriHub.Services.Interfaces
{
    public interface ISerialService
    {
        Task<int> ValidateSerialAsync(string materialCode, string serialNumber, string source);
        Task<int> UnfreezeSerialAsync(string materialCode, string serialNumber, string source);
    }
}
