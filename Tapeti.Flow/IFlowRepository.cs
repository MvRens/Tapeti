using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow
{
    public interface IFlowRepository
    {
        Task<IQueryable<FlowStateRecord>> GetAllStates();
        Task CreateState(Guid flowID, DateTime timestamp, string metadata, string data, IDictionary<Guid, string> continuations);
        Task UpdateState(Guid flowID, string metadata, string data, IDictionary<Guid, string> continuations);
        Task DeleteState(Guid flowID);
    }


    public class FlowStateRecord
    {
        public Guid FlowID;
        public string Metadata;
        public string Data;
        public Dictionary<Guid, string> Continuations;
    }
}
