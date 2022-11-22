using System;
using System.Collections.Generic;
using System.Linq;

namespace Tapeti.Flow.Default
{
    /// <summary>
    /// Represents the state stored for active flows.
    /// </summary>
    public class FlowState
    {
        private FlowMetadata metadata;
        private Dictionary<Guid, ContinuationMetadata> continuations;


        /// <summary>
        /// Contains metadata about the flow.
        /// </summary>
        public FlowMetadata Metadata
        {
            get => metadata ??= new FlowMetadata();
            set => metadata = value;
        }


        /// <summary>
        /// Contains the serialized state which is restored when a flow continues.
        /// </summary>
        public string Data { get; set; }


        /// <summary>
        /// Contains metadata about continuations awaiting a response.
        /// </summary>
        public Dictionary<Guid, ContinuationMetadata> Continuations
        {
            get => continuations ??= new Dictionary<Guid, ContinuationMetadata>();
            set => continuations = value;
        }


        /// <summary>
        /// Creates a deep clone of this FlowState.
        /// </summary>
        public FlowState Clone()
        {
            return new FlowState {
                metadata = metadata.Clone(),
                Data = Data,
                continuations = continuations?.ToDictionary(kv => kv.Key, kv => kv.Value.Clone())
            };
        }
    }


    /// <summary>
    /// Contains metadata about the flow.
    /// </summary>
    public class FlowMetadata
    {
        /// <summary>
        /// Contains information about the expected response for this flow.
        /// </summary>
        public ReplyMetadata Reply { get; set; }


        /// <summary>
        /// Creates a deep clone of this FlowMetadata.
        /// </summary>
        public FlowMetadata Clone()
        {
            return new FlowMetadata
            {
                Reply = Reply?.Clone()
            };
        }
    }


    /// <summary>
    /// Contains information about the expected response for this flow.
    /// </summary>
    public class ReplyMetadata
    {
        /// <summary>
        /// The queue to which the response should be sent.
        /// </summary>
        public string ReplyTo { get; set; }

        /// <summary>
        /// The correlation ID included in the original request.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// The expected response message class.
        /// </summary>
        public string ResponseTypeName { get; set; }

        /// <summary>
        /// Indicates whether the response should be sent a mandatory.
        /// False for requests originating from a dynamic queue.
        /// </summary>
        public bool Mandatory { get; set; }


        /// <summary>
        /// Creates a deep clone of this ReplyMetadata.
        /// </summary>
        public ReplyMetadata Clone()
        {
            return new ReplyMetadata
            {
                ReplyTo = ReplyTo,
                CorrelationId = CorrelationId,
                ResponseTypeName = ResponseTypeName,
                Mandatory = Mandatory
            };
        }
    }


    /// <summary>
    /// Contains metadata about a continuation awaiting a response.
    /// </summary>
    public class ContinuationMetadata
    {
        /// <summary>
        /// The name of the method which will handle the response.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// The name of the method which is called when all responses have been processed.
        /// </summary>
        public string ConvergeMethodName { get; set; }

        /// <summary>
        /// Determines if the converge method is synchronous or asynchronous.
        /// </summary>
        public bool ConvergeMethodSync { get; set; }


        /// <summary>
        /// Creates a deep clone of this ContinuationMetadata.
        /// </summary>
        public ContinuationMetadata Clone()
        {
            return new ContinuationMetadata
            {
                MethodName = MethodName,
                ConvergeMethodName = ConvergeMethodName,
                ConvergeMethodSync = ConvergeMethodSync
            };
        }
    }
}
