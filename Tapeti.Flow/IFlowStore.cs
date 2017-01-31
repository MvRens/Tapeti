using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Flow
{
    public interface IFlowStore
    {
        Task Load();
        Task<Guid?> FindFlowStateID(Guid continuationID);
        Task<IFlowStateLock> LockFlowState(Guid flowStateID);
    }

    public interface IFlowStateLock : IDisposable
    {
        Guid FlowStateID { get; }
        Task<FlowState> GetFlowState();
        Task StoreFlowState(FlowState flowState);
        Task DeleteFlowState();
    }

    public class FlowState
    {
        public string Metadata { get; set; }
        public string Data { get; set; }
        public Dictionary<Guid, string> Continuations { get; set; }
    }
}
