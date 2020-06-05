using System;
using BenchmarkDotNet.Running;

namespace Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary1 = BenchmarkRunner.Run<JobQueueBenchmark>();
            Console.ReadKey();
        }
    }
}
