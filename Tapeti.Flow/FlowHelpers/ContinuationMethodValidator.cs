using System;
using System.Collections.Generic;
using System.IO;
using Tapeti.Config;
using Tapeti.Flow.Default;
using Tapeti.Helpers;

namespace Tapeti.Flow.FlowHelpers
{
    /// <summary>
    /// Validates method names as used for Flow continuations. Can be used to ensure changes to the service do not break flows in progress.
    /// </summary>
    public class ContinuationMethodValidator
    {
        private readonly ITapetiConfig config;
        private readonly HashSet<string> validatedMethods = new();


        /// <inheritdoc cref="ContinuationMethodValidator"/>>
        public ContinuationMethodValidator(ITapetiConfig config)
        {
            this.config = config;
        }



        /// <summary>
        /// Validates the method names used the provided continuations, which are usually part of a <see cref="FlowState"/>.
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        public void ValidateContinuations(Guid flowId, IReadOnlyDictionary<Guid, ContinuationMetadata> continuations)
        {
            foreach (var pair in continuations)
                ValidateContinuation(flowId, pair.Key, pair.Value);
        }


        /// <summary>
        /// Validates the method names used in a single continuation.
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        public void ValidateContinuation(Guid flowId, Guid continuationId, ContinuationMetadata metadata)
        {
            if (string.IsNullOrEmpty(metadata.MethodName))
                return;

            // We could check all the things that are required for a continuation or converge method, but this should suffice
            // for the common scenario where you change code without realizing that it's signature has been persisted
            // ReSharper disable once InvertIf
            if (validatedMethods!.Add(metadata.MethodName))
            {
                var methodInfo = MethodSerializer.Deserialize(metadata.MethodName);
                if (methodInfo == null)
                    throw new InvalidDataException($"Flow ID {flowId} references continuation method '{metadata.MethodName}' which no longer exists (continuation Id = {continuationId})");

                var binding = config.Bindings.ForMethod(methodInfo);
                if (binding == null)
                    throw new InvalidDataException($"Flow ID {flowId} references continuation method '{metadata.MethodName}' which no longer has a binding as a message handler (continuation Id = {continuationId})");
            }

            /* Disabled for now - the ConvergeMethodName does not include the assembly so we can't easily check it
            if (string.IsNullOrEmpty(metadata.ConvergeMethodName) || !validatedMethods.Add(metadata.ConvergeMethodName))
                return;

            var convergeMethodInfo = MethodSerializer.Deserialize(metadata.ConvergeMethodName);
            if (convergeMethodInfo == null)
                throw new InvalidDataException($"Flow ID {flowId} references converge method '{metadata.ConvergeMethodName}' which no longer exists (continuation Id = {continuationId})");

            // Converge methods are not message handlers themselves
            */
        }
    }
}
