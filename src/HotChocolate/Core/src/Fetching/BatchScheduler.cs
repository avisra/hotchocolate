using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreenDonut;
using HotChocolate.Execution;
using HotChocolate.Resolvers;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace HotChocolate.Fetching
{
    /// <summary>
    /// The execution engine batch dispatcher.
    /// </summary>
    public class BatchScheduler
        : IBatchScheduler
        , IBatchDispatcher
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly object _sync = new();
        private readonly List<Func<ValueTask>> _tasks = new();
        private IExecutionTaskContext? _context;
        private bool _dispatchOnSchedule;

        /// <inheritdoc />
        public event EventHandler? TaskEnqueued;

        /// <inheritdoc />
        public bool HasTasks => _tasks.Count > 0;

        /// <inheritdoc />
        public bool DispatchOnSchedule
        {
            get => _dispatchOnSchedule;
            set
            {
                lock (_sync)
                {
                    _dispatchOnSchedule = value;
                }
            }
        }

        /// <inheritdoc />
        public void Initialize(IExecutionTaskContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc />
        public void Dispatch()
        {
            // TODO : resources
            IExecutionTaskContext context = _context ??
                throw new InvalidOperationException("The scheduler is not initialized.");

            lock (_sync)
            {
                if (_tasks.Count > 0)
                {
                    var task = new BatchExecutionTask(context, _tasks.ToList());
                    _tasks.Clear();
                    context.Register(task);
                }
            }
        }

        public void Schedule(Func<ValueTask> dispatch)
        {
            bool dispatchOnSchedule;

            lock (_sync)
            {
                if (_dispatchOnSchedule)
                {
                    dispatchOnSchedule = true;
                }
                else
                {
                    dispatchOnSchedule = false;
                    _tasks.Add(dispatch);
                    TaskEnqueued?.Invoke(this, EventArgs.Empty);
                }
            }

            if (dispatchOnSchedule)
            {
                BeginDispatchOnEnqueue(dispatch);
            }
        }

#pragma warning disable 4014
        private void BeginDispatchOnEnqueue(Func<ValueTask> dispatch) =>
            DispatchOnEnqueue(dispatch);
#pragma warning restore 4014

        private async Task DispatchOnEnqueue(Func<ValueTask> dispatch)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                await dispatch().ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private class BatchExecutionTask : ParallelExecutionTask
        {
            private readonly IReadOnlyList<Func<ValueTask>> _tasks;

            public BatchExecutionTask(
                IExecutionTaskContext context,
                IReadOnlyList<Func<ValueTask>> tasks)
            {
                Context = context;
                _tasks = tasks;
            }

            protected override IExecutionTaskContext Context { get; }

            protected override async ValueTask ExecuteAsync(CancellationToken cancellationToken)
            {
                var running = new Task[_tasks.Count];

                for (var i = 0; i < _tasks.Count; i++)
                {
                    running[i] = ExecuteBatchAsync(_tasks[i]);
                }

                await Task.WhenAll(running);

                async Task ExecuteBatchAsync(Func<ValueTask> executeBatchAsync)
                {
                    try
                    {
                        await executeBatchAsync.Invoke().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // If we run into this exception the request was aborted.
                        // In this case we do nothing and just return.
                    }
                    catch (Exception ex)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            // if cancellation is request we do no longer report errors to the
                            // operation context.
                            return;
                        }

                        Context.ReportError(this, ex);
                    }
                }
            }
        }
    }
}
