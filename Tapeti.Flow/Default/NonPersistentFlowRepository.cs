using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    public class NonPersistentFlowRepository : IFlowRepository
    {
        public Task<IQueryable<FlowStateRecord>> GetStates()
        {
            return Task.FromResult(new List<FlowStateRecord>().AsQueryable());
        }

        public Task CreateState(FlowStateRecord stateRecord, DateTime timestamp)
        {
            return Task.CompletedTask;
        }

        public Task UpdateState(FlowStateRecord stateRecord)
        {
            return Task.CompletedTask;
        }

        public Task DeleteState(Guid flowID)
        {
            return Task.CompletedTask;
        }
    }
}
