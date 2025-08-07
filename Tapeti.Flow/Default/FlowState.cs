using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace Tapeti.Flow.Default
{
    // Note: FlowState used to be modifiable and have a Clone method, but that requires constant vigilance
    // when dealing with the cache to prevent the wrong instance from being overwritten. So I refactored it to
    // be a proper value object with a few helpers to create new versions, which greatly reduces the chance of more bugs.
    //
    // This is less than ideal if there are many modifications. Perhaps a Copy-on-Write or Builder pattern would
    // be better. For now however we don't have use cases with more than a few continuations, so the overhead
    // is tiny.


    /// <summary>
    /// Represents the state stored for active flows.
    /// </summary>
    public class FlowState
    {
        private FlowMetadata metadata = null!;
        private IReadOnlyDictionary<Guid, ContinuationMetadata> continuations = null!;


        /// <summary>
        /// Contains metadata about the flow.
        /// </summary>
        public required FlowMetadata Metadata
        {
            get => metadata;
            init => metadata = value;
        }


        /// <summary>
        /// Contains the serialized state which is restored when a flow continues.
        /// </summary>
        public required string? Data { get; init; }


        /// <summary>
        /// Contains metadata about continuations awaiting a response.
        /// </summary>
        public required IReadOnlyDictionary<Guid, ContinuationMetadata> Continuations
        {
            get => continuations;
            init => continuations = value;
        }


        /// <summary>
        /// Returns a clone of this FlowState with new Data.
        /// </summary>
        public FlowState WithData(string data)
        {
            return new FlowState
            {
                Metadata = Metadata,
                Data = data,
                Continuations = Continuations
            };
        }


        /// <summary>
        /// Returns a clone of this FlowState with the specified Continuation added.
        /// </summary>
        public FlowState WithContinuation(Guid continuationID, ContinuationMetadata continuationMetadata)
        {
            return new FlowState
            {
                Metadata = Metadata,
                Data = Data,
                Continuations = Continuations.Append(new KeyValuePair<Guid, ContinuationMetadata>(continuationID, continuationMetadata)).ToDictionary()
            };
        }


        /// <summary>
        /// Returns a clone of this FlowState with the specified Continuation removed.
        /// </summary>
        public FlowState WithoutContinuation(Guid continuationID)
        {
            return new FlowState
            {
                Metadata = Metadata,
                Data = Data,
                Continuations = Continuations.Where(p => p.Key != continuationID).ToDictionary()
            };
        }


        [OnDeserialized]
        [PublicAPI]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            // Deserialization is the only way these fields should be able to end up as null. It should never happen
            // or have happened in the past, but older code handled it gracefully so might as well keep that.
            // And it's a great excuse to not fix the Flow.SQL unit test which uses an empty JSON object ;-)
            // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            metadata ??= new FlowMetadata(null);
            continuations ??= new Dictionary<Guid, ContinuationMetadata>();
            // ReSharper restore NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
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
        public ReplyMetadata? Reply { get; }


        /// <inheritdoc cref="FlowMetadata"/>
        public FlowMetadata(ReplyMetadata? reply)
        {
            Reply = reply;
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
        public required string? ReplyTo { get; init; }

        /// <summary>
        /// The correlation ID included in the original request.
        /// </summary>
        public required string? CorrelationId { get; init; }

        /// <summary>
        /// The expected response message class.
        /// </summary>
        public required string? ResponseTypeName { get; init; }

        /// <summary>
        /// Indicates whether the response should be sent a mandatory.
        /// False for requests originating from a dynamic queue.
        /// </summary>
        public required bool Mandatory { get; init; }
    }


    /// <summary>
    /// Contains metadata about a continuation awaiting a response.
    /// </summary>
    public class ContinuationMetadata
    {
        private string? methodName;


        /// <summary>
        /// The name of the method which will handle the response.
        /// </summary>
        public required string? MethodName
        {
            get => methodName;
            init => methodName = value;
        }

        /// <summary>
        /// The name of the method which is called when all responses have been processed.
        /// </summary>
        public required string? ConvergeMethodName { get; init; }

        /// <summary>
        /// Determines if the converge method is synchronous or asynchronous.
        /// </summary>
        public required bool ConvergeMethodSync { get; init; }


        /// <summary>
        /// Used to map the method name to a new value for backwards compatibility when loading in a flow state.
        /// </summary>
        public void MapMethodName(string? newMethodName)
        {
            methodName = newMethodName;
        }
    }
}
