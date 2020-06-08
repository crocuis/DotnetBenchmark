using System;
using BenchmarkDotNet.Running;

namespace Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            //var summary1 = BenchmarkRunner.Run<JobQueueBenchmark>();

            var instance = new JobQueueBenchmark();
            instance.ActionBlockQueue();
            instance.ActionBlockQueueParallel();

            instance.ActionBlockQueueDedicatedThreadPool();
            instance.ActionBlockQueueDedicatedThreadPoolParallel();
            
            instance.ChannelsQueue();
            instance.ChannelsQueueParallel();

            instance.BlockingCollectionQueue();
            instance.BlockingCollectionQueueParallel();
            instance.ChannelsQueueDedicatedThreadParallel();
            

            //Console.Write("Complete Benchmark");
            Console.ReadKey();
        }
    }
}
