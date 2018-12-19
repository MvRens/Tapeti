using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Flow
{
    public interface IFlowRepository
    {
        Task<List<KeyValuePair<Guid, T>>> GetStates<T>();
        Task CreateState<T>(Guid flowID, T state, DateTime timestamp);
        Task UpdateState<T>(Guid flowID, T state);
        Task DeleteState(Guid flowID);
    }
}
