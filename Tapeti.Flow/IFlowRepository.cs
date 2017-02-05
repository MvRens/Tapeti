using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow
{
    public interface IFlowRepository
    {
        Task<IQueryable<FlowStateRecord>> GetStates();
        Task CreateState(FlowStateRecord stateRecord, DateTime timestamp);
        Task UpdateState(FlowStateRecord stateRecord);
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
