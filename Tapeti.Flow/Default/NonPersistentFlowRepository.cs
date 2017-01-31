using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    public class NonPersistentFlowRepository : IFlowRepository
    {
        public Task<IQueryable<FlowStateRecord>> GetAllStates()
        {
            return Task.FromResult(new List<FlowStateRecord>().AsQueryable());
        }

        public Task CreateState(Guid flowID, DateTime timestamp, string metadata, string data, IDictionary<Guid, string> continuations)
        {
            return Task.CompletedTask;
        }

        public Task UpdateState(Guid flowID, string metadata, string data, IDictionary<Guid, string> continuations)
        {
            return Task.CompletedTask;
        }

        public Task DeleteState(Guid flowID)
        {
            return Task.CompletedTask;
        }
    }
}
