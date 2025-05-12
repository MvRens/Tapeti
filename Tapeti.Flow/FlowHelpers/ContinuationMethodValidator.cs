using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Tapeti.Config;
using Tapeti.Flow.Default;
using Tapeti.Helpers;

namespace Tapeti.Flow.FlowHelpers
{
    /// <summary>
    /// Represents a continuation method previously persisted in the Flow Store.
    /// </summary>
    [PublicAPI]
    public interface IStoredContinuationMethod
    {
        /// <summary>
        /// The serialized method name as stored.
        /// </summary>
        /// <remarks>
        /// In the format as specified by <see cref="MethodSerializer"/>.
        /// </remarks>
        string SerializedMethodName { get; }


        /// <summary>
        /// The name of the assembly parsed from the <see cref="SerializedMethodName"/>.
        /// </summary>
        string AssemblyName { get; }

        /// <summary>
        /// The declaring type name parsed from the <see cref="SerializedMethodName"/>.
        /// </summary>
        string DeclaringTypeName { get; }

        /// <summary>
        /// The method name parsed from the <see cref="SerializedMethodName"/>.
        /// </summary>
        string MethodName { get; }


        /// <summary>
        /// Maps this continuation method to another method. Use when the assembly, class or method has been renamed in
        /// a refactoring.
        /// </summary>
        /// <remarks>
        /// The new method is only partially validated to be a valid continuation method. You are responsible for making
        /// sure the new method belongs to a compatible controller (either the same or renamed) and has the proper signature!
        /// </remarks>
        void MapTo<TController>(Expression<Func<TController, Delegate>> methodSelector) where TController : class;
    }


    /// <summary>
    /// Called whenever a method name needs to be resolved. This allows for backwards compatibility when
    /// a stored flow still references the old method name.
    /// </summary>
    /// <param name="method"></param>
    public delegate void ContinuationMethodMapperProc(IStoredContinuationMethod method);



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


    /// <summary>
    /// Factory interface for <see cref="IContinuationMethodValidator"/>.
    /// </summary>
    public interface IContinuationMethodValidatorFactory
    {
        /// <summary>
        /// Returns an <see cref="IContinuationMethodValidator"/> implementation with the specified mapper callback.
        /// </summary>
        IContinuationMethodValidator Create(ContinuationMethodMapperProc? mapper);
    }


    /// <summary>
    /// Validates method names as used for Flow continuations. Can be used to ensure changes to the service do not break flows in progress.
    /// </summary>
    public class ContinuationMethodValidator : IContinuationMethodValidator
    {
        private readonly ITapetiConfig config;
        private readonly ContinuationMethodMapperProc? mapper;
        private readonly Dictionary<string, string> validatedMethods = [];


        /// <inheritdoc cref="ContinuationMethodValidator"/>>
        /// <param name="config">The Tapeti configuration used for validating message handlers.</param>
        /// <param name="mapper">See documentation for <see cref="ContinuationMethodMapperProc"/>.</param>
        public ContinuationMethodValidator(ITapetiConfig config, ContinuationMethodMapperProc? mapper = null)
        {
            this.config = config;
            this.mapper = mapper;
        }



        /// <inheritdoc />
        public void ValidateContinuations(Guid flowId, IReadOnlyDictionary<Guid, ContinuationMetadata> continuations)
        {
            foreach (var pair in continuations)
                pair.Value.MethodName = ValidateMethodName(flowId, pair.Key, pair.Value.MethodName);
        }


        /// <inheritdoc />
        public string? ValidateMethodName(Guid flowId, Guid continuationId, string? methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                return methodName;

            // We could check all the things that are required for a continuation or converge method, but this should suffice
            // for the common scenario where you change code without realizing that it's signature has been persisted
            if (validatedMethods.TryGetValue(methodName, out var resolvedMethodName))
                return resolvedMethodName;


            resolvedMethodName = methodName;
            MethodInfo? methodInfo = null;

            if (mapper is not null)
                (methodInfo, resolvedMethodName) = new StoredContinuationMethod(methodName).Apply(mapper);

            validatedMethods.Add(methodName, resolvedMethodName);


            if (methodInfo is null)
            {
                methodInfo = MethodSerializer.Deserialize(methodName);
                if (methodInfo == null)
                    throw new InvalidDataException($"Flow ID {flowId} references continuation method '{methodName}' which no longer exists (continuation Id = {continuationId})");
            }

            var binding = config.Bindings.ForMethod(methodInfo);
            if (binding == null)
                throw new InvalidDataException($"Flow ID {flowId} references continuation method '{resolvedMethodName}' which no longer has a binding as a message handler (continuation Id = {continuationId})");

            return resolvedMethodName;
        }


        /* Disabled for now - the ConvergeMethodName does not include the assembly so we can't easily check it
        if (string.IsNullOrEmpty(metadata.ConvergeMethodName) || !validatedMethods.Add(metadata.ConvergeMethodName))
            return;

        var convergeMethodInfo = MethodSerializer.Deserialize(metadata.ConvergeMethodName);
        if (convergeMethodInfo == null)
            throw new InvalidDataException($"Flow ID {flowId} references converge method '{metadata.ConvergeMethodName}' which no longer exists (continuation Id = {continuationId})");

        // Converge methods are not message handlers themselves
        */



        private class StoredContinuationMethod : IStoredContinuationMethod
        {
            public string SerializedMethodName { get; }
            public string AssemblyName { get; }
            public string DeclaringTypeName { get; }
            public string MethodName { get; }

            private MethodInfo? mappedMethodInfo;


            public StoredContinuationMethod(string serializedMethodName)
            {
                SerializedMethodName = serializedMethodName;

                MethodSerializer.TryDeconstruct(serializedMethodName, out var assemblyName, out var declaringTypeName, out var methodName);
                AssemblyName = assemblyName;
                DeclaringTypeName = declaringTypeName;
                MethodName = methodName;
            }


            public void MapTo<TController>(Expression<Func<TController, Delegate>> methodSelector) where TController : class
            {
                var callExpression = (methodSelector.Body as UnaryExpression)?.Operand as MethodCallExpression;
                var targetMethodExpression = callExpression?.Object as ConstantExpression;

                mappedMethodInfo = targetMethodExpression?.Value as MethodInfo;
                if (mappedMethodInfo == null)
                    throw new ArgumentException("Unable to determine the method", nameof(methodSelector));
            }


            public (MethodInfo? methodInfo, string resolvedMethodName) Apply(ContinuationMethodMapperProc mapperProc)
            {
                mapperProc(this);

                return mappedMethodInfo is not null
                    ? (mappedMethodInfo, MethodSerializer.Serialize(mappedMethodInfo))
                    : (null, SerializedMethodName);
            }
        }
    }


    /// <inheritdoc />
    public class ContinuationMethodValidatorFactory : IContinuationMethodValidatorFactory
    {
        private readonly ITapetiConfig config;


        /// <inheritdoc cref="ContinuationMethodValidatorFactory" />
        public ContinuationMethodValidatorFactory(ITapetiConfig config)
        {
            this.config = config;
        }


        /// <inheritdoc />
        public IContinuationMethodValidator Create(ContinuationMethodMapperProc? mapper)
        {
            return new ContinuationMethodValidator(config, mapper);
        }
    }
}
