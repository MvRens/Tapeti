using BenchmarkDotNet.Running;
using Tapeti.Benchmarks.Tests;

BenchmarkRunner.Run<MethodInvokeBenchmarks>();
//new MethodInvokeBenchmarks().InvokeExpressionValueFactory();