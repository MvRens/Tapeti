using System.Threading.Tasks;

namespace Tapeti.Saga
{
    public interface ISagaProvider
    {
        Task<ISaga<T>> Begin<T>(T initialState) where T : class;
        Task<ISaga<T>> Continue<T>(string sagaId) where T : class;
        Task<object> Continue(string sagaId);
    }
}
