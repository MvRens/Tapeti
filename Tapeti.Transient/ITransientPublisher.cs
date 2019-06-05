using System.Threading.Tasks;

namespace Tapeti.Transient
{
    public interface ITransientPublisher
    {
        Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request);
    }
}