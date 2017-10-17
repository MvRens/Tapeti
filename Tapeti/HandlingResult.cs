using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tapeti
{
    public class HandlingResult
    {
        public HandlingResult()
        {
            ConsumeResponse = ConsumeResponse.Nack;
            MessageAction = MessageAction.None;
        }

        /// <summary>
        /// Determines which response will be given to the message bus from where the message originates.
        /// </summary>
        public ConsumeResponse ConsumeResponse { get; internal set; }

        /// <summary>
        /// Registers which action the Exception strategy has taken or will take to handle the error condition
        /// on the message. This is important to know for cleanup handlers registered by middleware.
        /// </summary>
        public MessageAction MessageAction { get; internal set; }

    }

    public class HandlingResultBuilder
    {
        private static readonly HandlingResult Default = new HandlingResult();

        private HandlingResult data = Default;

        public ConsumeResponse ConsumeResponse {
            get
            {
                return data.ConsumeResponse;
            }
            set
            {
                GetWritableData().ConsumeResponse = value;
            }
        }

        public MessageAction MessageAction
        {
            get
            {
                return data.MessageAction;
            }
            set
            {
                GetWritableData().MessageAction = value;
            }
        }

        public HandlingResult ToHandlingResult()
        {
            if (data == Default)
            {
                return new HandlingResult();
            }
            var result = GetWritableData();
            data = Default;
            return result;
        }

        private HandlingResult GetWritableData()
        {
            if (data == Default)
            {
                data = new HandlingResult();
            }
            return data;
        }
    }
}
