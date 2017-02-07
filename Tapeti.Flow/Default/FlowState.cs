using System;
using System.Collections.Generic;
using System.Linq;

namespace Tapeti.Flow.Default
{
    public class FlowState
    {
        private FlowMetadata metadata;
        private Dictionary<Guid, ContinuationMetadata> continuations;


        public FlowMetadata Metadata
        {
            get { return metadata ?? (metadata = new FlowMetadata()); }
            set { metadata = value; }
        }

        public string Data { get; set; }

        public Dictionary<Guid, ContinuationMetadata> Continuations
        {
            get { return continuations ?? (continuations = new Dictionary<Guid, ContinuationMetadata>()); }
            set { continuations = value; }
        }


        public void Assign(FlowState value)
        {
            Metadata = value.Metadata.Clone();
            Data = value.Data;
            Continuations = value.Continuations.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
        }


        public FlowState Clone()
        {
            var result = new FlowState();
            result.Assign(this);

            return result;
        }
    }


    public class FlowMetadata
    {
        public ReplyMetadata Reply { get; set; }


        public FlowMetadata Clone()
        {
            return new FlowMetadata
            {
                Reply = Reply?.Clone()
            };
        }
    }


    public class ReplyMetadata
    {
        public string ReplyTo { get; set; }
        public string CorrelationId { get; set; }
        public string ResponseTypeName { get; set; }


        public ReplyMetadata Clone()
        {
            return new ReplyMetadata
            {
                ReplyTo = ReplyTo,
                CorrelationId = CorrelationId,
                ResponseTypeName = ResponseTypeName
            };
        }
    }


    public class ContinuationMetadata
    {
        public string MethodName { get; set; }
        public string ConvergeMethodName { get; set; }


        public ContinuationMetadata Clone()
        {
            return new ContinuationMetadata
            {
                MethodName = MethodName,
                ConvergeMethodName = ConvergeMethodName
            };
        }
    }
}
