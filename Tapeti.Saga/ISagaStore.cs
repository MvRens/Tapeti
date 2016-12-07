using System.Threading.Tasks;

namespace Tapeti.Saga
{
    public interface ISagaStore
    {
        Task<object> Read(string sagaId);
        Task Update(string sagaId, object state);
    }
}
