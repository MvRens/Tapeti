using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tapeti.Flow.Default;

namespace Tapeti.Flow
{
    public interface IFlowStore
    {
        Task Load();
        Task<Guid?> FindFlowID(Guid continuationID);
        Task<IFlowStateLock> LockFlowState(Guid flowID);
    }

    public interface IFlowStateLock : IDisposable
    {
        Guid FlowID { get; }

        Task<FlowState> GetFlowState();
        Task StoreFlowState(FlowState flowState);
        Task DeleteFlowState();
    }
}
