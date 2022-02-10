using System.Reflection;
using BenchmarkDotNet.Attributes;
using Tapeti.Helpers;

#pragma warning disable CA1822 // Mark members as static - required for Benchmark.NET

namespace Tapeti.Benchmarks.Tests
{
    [MemoryDiagnoser]
    public class MethodInvokeBenchmarks
    {
        private delegate bool MethodToInvokeDelegate(object obj);

        private static readonly MethodInfo MethodToInvokeInfo;
        private static readonly MethodToInvokeDelegate MethodToInvokeDelegateInstance;
        private static readonly ExpressionInvoke MethodToInvokeExpression;


        static MethodInvokeBenchmarks()
        {
            MethodToInvokeInfo = typeof(MethodInvokeBenchmarks).GetMethod(nameof(MethodToInvoke))!;

            var inputInstance = new MethodInvokeBenchmarks();
            MethodToInvokeDelegateInstance = i => ((MethodInvokeBenchmarks)i).MethodToInvoke(inputInstance.GetSomeObject(), inputInstance.GetCancellationToken());
            MethodToInvokeExpression = MethodToInvokeInfo.CreateExpressionInvoke();

            /*

            Fun experiment, but a bit too tricky for me at the moment.


            var dynamicMethodToInvoke = new DynamicMethod(
                nameof(MethodToInvoke),
                typeof(bool),
                new[] { typeof(object) },
                typeof(MethodInvokeBenchmarks).Module);


            var generator = dynamicMethodToInvoke.GetILGenerator(256);

            generator.Emit(OpCodes.Ldarg_0); // Load the first argument (the instance) onto the stack
            generator.Emit(OpCodes.Castclass, typeof(MethodInvokeBenchmarks)); // Cast to the expected instance type 
            generator.Emit(OpCodes.Ldc_I4_S, 42); // Push the first argument onto the stack
            generator.EmitCall(OpCodes.Callvirt, MethodToInvokeInfo, null); // Call the method
            generator.Emit(OpCodes.Ret);

            MethodToInvokeEmitted = dynamicMethodToInvoke.CreateDelegate<MethodToInvokeDelegate>();
            */
        }


        public bool MethodToInvoke(object someObject, CancellationToken cancellationToken)
        {
            return true;
        }


        // ReSharper disable MemberCanBeMadeStatic.Local
        private object GetSomeObject()
        {
            return new object();
        }


        private CancellationToken GetCancellationToken()
        {
            return CancellationToken.None;
        }
        // ReSharper restore MemberCanBeMadeStatic.Local



        // For comparison
        [Benchmark]
        public bool Direct()
        {
            return MethodToInvoke(GetSomeObject(), GetCancellationToken());
        }


        // For comparison as well, as we don't know the signature beforehand
        [Benchmark]
        public bool Delegate()
        {
            var instance = new MethodInvokeBenchmarks();
            return MethodToInvokeDelegateInstance(instance);
        }


        [Benchmark]
        public bool MethodInvoke()
        {
            var instance = new MethodInvokeBenchmarks();
            return (bool)(MethodToInvokeInfo.Invoke(instance, BindingFlags.DoNotWrapExceptions, null, new[] { GetSomeObject(), GetCancellationToken() }, null) ?? false);
        }


        [Benchmark]
        public bool InvokeExpression()
        {
            var instance = new MethodInvokeBenchmarks();
            return (bool)MethodToInvokeExpression(instance, GetSomeObject(), GetCancellationToken());
        }

        //[Benchmark]
        //public bool ReflectionEmit()
        //{
        //    var instance = new MethodInvokeBenchmarks();
        //    return MethodToInvokeEmitted(instance);
        //}
    }
}
