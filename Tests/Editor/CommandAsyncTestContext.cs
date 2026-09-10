using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;

namespace Deucarian.CommandRouting.Tests
{
    /// <summary>
    /// A pumped caller context: completion occurs with no current context,
    /// then only the test's owner thread is allowed to drain posted work.
    /// No background worker is needed to exercise the context-bound path.
    /// </summary>
    internal sealed class CommandAsyncTestContext : SynchronizationContext,
        IDisposable
    {
        private readonly SynchronizationContext previous;
        private readonly int ownerThread;
        private readonly Queue<Action> work = new Queue<Action>();
        private readonly object gate = new object();
        private int operations;
        private int completedOffContext;

        public CommandAsyncTestContext()
        {
            previous = Current;
            ownerThread = Thread.CurrentThread.ManagedThreadId;
            SetSynchronizationContext(this);
        }

        public int PendingOperations => Volatile.Read(ref operations);
        public int CompletedOffContext => Volatile.Read(ref completedOffContext);

        public override void OperationStarted() =>
            Interlocked.Increment(ref operations);

        public override void OperationCompleted()
        {
            if (!ReferenceEquals(Current, this))
                Interlocked.Increment(ref completedOffContext);
            Interlocked.Decrement(ref operations);
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            lock (gate) { work.Enqueue(() => callback(state)); }
        }

        public void PumpUntil(Func<bool> completed)
        {
            Assert.That(Thread.CurrentThread.ManagedThreadId,
                Is.EqualTo(ownerThread));
            var timer = Stopwatch.StartNew();
            while (!completed())
            {
                Action next = null;
                lock (gate)
                {
                    if (work.Count > 0) next = work.Dequeue();
                }
                if (next != null)
                {
                    SynchronizationContext saved = Current;
                    SetSynchronizationContext(this);
                    try { next(); }
                    finally { SetSynchronizationContext(saved); }
                }
                else
                {
                    Assert.That(timer.Elapsed.TotalSeconds, Is.LessThan(3),
                        "The pending command did not complete on the pumped caller context.");
                    Thread.Sleep(1);
                }
            }
        }

        public static void WithoutContext(Action complete)
        {
            SynchronizationContext saved = Current;
            SetSynchronizationContext(null);
            try { complete(); }
            finally { SetSynchronizationContext(saved); }
        }

        public void Dispose() => SetSynchronizationContext(previous);
    }
}
