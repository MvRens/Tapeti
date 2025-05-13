using System;
using System.Collections.Generic;
using System.IO;
using Tapeti.Flow.Default;

namespace Tapeti.Flow.Validation
{
    /// <summary>
    /// Abstracts the continuation method validator for unit testing purposes.
    /// </summary>
    public interface IContinuationMethodValidator
    {
        /// <summary>
        /// Validates the method names used the provided continuations, which are usually part of a <see cref="FlowState"/>.
        /// </summary>
        /// <remarks>
        /// Note: if the mapper applies any changes to a continuation, the ContinuationMetadata is modified in place.
        /// </remarks>
        /// <exception cref="InvalidDataException"></exception>
        void ValidateContinuations(Guid flowId, IReadOnlyDictionary<Guid, ContinuationMetadata> continuations);

        /// <summary>
        /// Validates the method names used in a single continuation.
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        string? ValidateMethodName(Guid flowId, Guid continuationId, string? methodName);
    }
}
