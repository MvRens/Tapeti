using System;
using System.Collections.Generic;
using System.Linq;
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


    public class FlowStateRecord
    {
        public Guid FlowID;
        public string Metadata;
        public string Data;
        public Dictionary<Guid, string> ContinuationMetadata;
    }
}
