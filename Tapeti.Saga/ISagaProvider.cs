using System.Threading.Tasks;

namespace Tapeti.Saga
{
    public interface ISagaProvider
    {
        Task<ISaga<T>> Begin<T>() where T : class;
        Task<ISaga<T>> Continue<T>(string sagaId) where T : class;
        Task<ISaga<T>> Current<T>() where T : class;
    }
}
