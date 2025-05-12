using System;
using System.Collections.Generic;
using Tapeti.Flow.Default;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Tests.Mock
{
    internal class MockContinuationMethodValidatorFactory : IContinuationMethodValidatorFactory
    {
        public IContinuationMethodValidator Create(ContinuationMethodMapperProc? mapper)
        {
            return new MockContinuationMethodValidator();
        }
    }


    internal class MockContinuationMethodValidator : IContinuationMethodValidator
    {
        public void ValidateContinuations(Guid flowId, IReadOnlyDictionary<Guid, ContinuationMetadata> continuations)
        {
        }


        public string? ValidateMethodName(Guid flowId, Guid continuationId, string? methodName)
        {
            return methodName;
        }
    }
}
