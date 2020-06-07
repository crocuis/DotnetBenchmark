using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Perfolizer.Horology;

namespace Benchmark
{
    [MemoryDiagnoser]

    public class JobQueueBenchmark
    {

        private readonly AutoResetEvent _autoResetEvent;

        public JobQueueBenchmark()
        {
            _autoResetEvent = new AutoResetEvent(false);
        }
        [Benchmark]
        public void BlockingCollectionQueue()
        {
            DoManyJobs(new BlockingCollectionQueue());

        }

        [Benchmark]
        public void ChannelsQueue()
        {
            DoManyJobs(new ChannelsQueue());
        }
        [Benchmark]
        public void ChannelsQueueDedicatedThread()
        {
            DoManyJobs(new ChannelsQueueDedicatedThread());
        }
        [Benchmark]
        public void ActionBlockQueue()
        {
            DoManyJobs(new ActionBlockQueue());
        }

        [Benchmark]
        public void ActionBlockQueueParallel()
        {
            DoManyJobsParallel(new ActionBlockQueue());
        }

        [Benchmark]
        public void ChannelsQueueParallel()
        {
            DoManyJobsParallel(new ChannelsQueue());
        }

        [Benchmark]
        public void ChannelsQueueDedicatedThreadParallel()
        {
            DoManyJobsParallel(new ChannelsQueueDedicatedThread());
        }

        [Benchmark]
        public void BlockingCollectionQueueParallel()
        {
            DoManyJobsParallel(new BlockingCollectionQueue());
        }

        private void DoManyJobs<T>(T jobQueue) where T : IJobQueue<Job>
        {
            int jobs = 10_000_000;
            var result = new ConcurrentBag<ClockSpan>();
            for (int i = 0; i < jobs - 1; i++)
            {
                jobQueue.Enqueue(new Job(i, (idx, clockSpan) =>
                {
                    result.Add(clockSpan);
                }));
            }

            jobQueue.Enqueue(new Job(jobs, (_, __) => _autoResetEvent.Set()));
            _autoResetEvent.WaitOne();
            jobQueue.Stop();

            var minSeconds = result.Min(p => p.GetSeconds());
            var maxSeconds = result.Max(p => p.GetSeconds());
            var avgSeconds = result.Average(p => p.GetSeconds());
            Console.WriteLine($"DoManyJobs, {typeof(T).Name}, MinSeconds: {minSeconds}, MaxSeconds: {maxSeconds}, AvgSeconds: {avgSeconds}");
        }

        private void DoManyJobsParallel<T>(T jobQueue) where T : IJobQueue<Job>
        {
            int jobs = 10_000_000;
            var result = new ConcurrentBag<ClockSpan>();
            Parallel.For(0, jobs, (index) =>
            {
                jobQueue.Enqueue(new Job(index, (idx, clockSpan) => { result.Add(clockSpan); }));
            });
            jobQueue.Enqueue(new Job(jobs, (_, __) => _autoResetEvent.Set()));
            _autoResetEvent.WaitOne();
            jobQueue.Stop();

            var minSeconds = result.Min(p => p.GetSeconds());
            var maxSeconds = result.Max(p => p.GetSeconds());
            var avgSeconds = result.Average(p => p.GetSeconds());
            Console.WriteLine($"DoManyJobsParallel, {typeof(T).Name}, MinSeconds: {minSeconds}, MaxSeconds: {maxSeconds}, AvgSeconds: {avgSeconds}");
        }
    }

    internal interface IJobQueue<in T>
    {
        void Enqueue(T job);
        void Stop();
    }

    public struct Job
    {
        public Job(Int32 idx, Action<int, ClockSpan> action)
        {
            Action = action;
            StartedClock = Chronometer.Start();
            Index = idx;
        }

        public void Dispatch()
        {
            Action(Index, StartedClock.GetElapsed());
        }

        public Action<int, ClockSpan> Action;
        public StartedClock StartedClock;
        public int Index;
    }


    public class BlockingCollectionQueue : IJobQueue<Job>
    {
        private readonly BlockingCollection<Job> _jobs = new BlockingCollection<Job>(new ConcurrentQueue<Job>());

        public BlockingCollectionQueue()
        {
            var thread = new Thread(new ThreadStart(OnStart));
            thread.IsBackground = true;
            thread.Start();
        }

        public void Enqueue(Job job)
        {
            _jobs.Add(job);
        }

        private void OnStart()
        {
            foreach (var job in _jobs.GetConsumingEnumerable(CancellationToken.None))
            {
                job.Dispatch();
            }
        }
        public void Stop()
        {
            _jobs.CompleteAdding();
        }
    }

    public class ChannelsQueue : IJobQueue<Job>
    {
        private readonly ChannelWriter<Job> _writer;

        public ChannelsQueue()
        {
            var channel = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions() { SingleReader = true });
            var reader = channel.Reader;
            _writer = channel.Writer;


            Task.Run(async () =>
            {
                while (await reader.WaitToReadAsync())
                {
                    // Fast loop around available jobs
                    while (reader.TryRead(out var job))
                    {
                        OnStart(job);
                    }
                }
            });
        }

        public void OnStart(Job job)
        {
            job.Dispatch();
        }

        public void Enqueue(Job job)
        {
            _writer.TryWrite(job);
        }

        public void Stop()
        {
            _writer.Complete();
        }
    }
    public class ChannelsQueueDedicatedThread : IJobQueue<Job>
    {
        private readonly ChannelWriter<Job> _writer;

        public ChannelsQueueDedicatedThread()
        {
            var channel = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions() { SingleReader = true });
            _writer = channel.Writer;
            var reader = channel.Reader;


            var thread = new Thread(new ThreadStart(() => Start(reader)));
            thread.IsBackground = true;
            thread.Start();
        }

        public async void Start(ChannelReader<Job> reader)
        {
            while (await reader.WaitToReadAsync())
            {
                // Fast loop around available jobs
                while (reader.TryRead(out var job))
                {
                    job.Dispatch();
                }
            }
        }

        public void Enqueue(Job job)
        {
            _writer.TryWrite(job);
        }

        public void Stop()
        {
            _writer.Complete();
        }
    }

    public class ActionBlockQueue : IJobQueue<Job>
    {
        private readonly ActionBlock<Job> _writer;

        public ActionBlockQueue()
        {
            _writer = new ActionBlock<Job>(Dispatch);
        }
        public void Enqueue(Job job)
        {
            _writer.Post(job);
        }

        public void Dispatch(Job job)
        {
            job.Dispatch();
        }

        public void Stop()
        {
            _writer.Complete();
        }
    }
}
