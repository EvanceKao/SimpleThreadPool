using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;

namespace SimpleThreadPool
{
    public class SimpleThreadPool : ISimpleThreadPool, IDisposable
    {
        private bool _disposedValue;


        private ConcurrentBag<ThreadEntity> _threadEntities = new ConcurrentBag<ThreadEntity>();
        private ConcurrentQueue<WorkItem> _workItems = new ConcurrentQueue<WorkItem>();
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;

        private int _maxThreadCount;
        private ThreadPriority _threadPriority;
        private int _threadWaitJobMillisecondsTimeout;

        public SimpleThreadPool(int maxThreadCount = 10, int defaultThreadCount = 1, ThreadPriority threadPriority = ThreadPriority.Normal, int threadWaitJobMillisecondsTimeout = 30000)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            _maxThreadCount = maxThreadCount;
            _threadPriority = threadPriority;
            _threadWaitJobMillisecondsTimeout = threadWaitJobMillisecondsTimeout;


            for (int i = 0; i < defaultThreadCount; i++)
            {
                _threadEntities.Add(new ThreadEntity(_workItems, _threadPriority, _threadWaitJobMillisecondsTimeout, _cancellationToken));
            }
        }

        public bool QueueUserWorkItem(WaitCallback waitCallback)
        {
            return this.QueueUserWorkItem(waitCallback, null);
        }

        public bool QueueUserWorkItem(WaitCallback waitCallback, object? state = null)
        {
            //if (_cancellationToken.IsCancellationRequested)
            //{
            //    // 本店已關
            //    return false;
            //}


            // 找看看有沒有可用的工人，沒工人可用的話就 new 個新的
            //if (_threadEntities.Count < _maxThreadCount && _threadEntities.All(x => x.IsBusy))
            // 還有工作沒消化完，且工人數量還沒達到上限就 new 個新的工人
            if (_workItems.Count > 0 && _threadEntities.Count < _maxThreadCount)
            {
                _threadEntities.Add(new ThreadEntity(_workItems, _threadPriority, _threadWaitJobMillisecondsTimeout, _cancellationToken));
            }

            // 把工作塞進去佇列
            _workItems.Enqueue(new WorkItem()
            {
                WaitCallback = waitCallback,
                State = state
            });

            // 叫工人們起床
            WakeUpAllThreads();

            return true;
        }

        public void EndPool()
        {
            this.EndPool(false);
        }

        public void CancelPool()
        {
            this.EndPool(true);
        }

        public void EndPool(bool cancelQueueItem)
        {
            if (_threadEntities.Count == 0)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            WakeUpAllThreads();

            _threadEntities.Clear();

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        private void WakeUpAllThreads()
        {
            Parallel.ForEach(_threadEntities, x => x.WakeUp());
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~SimpleThreadPool()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }


    public class WorkItem
    {
        public WaitCallback WaitCallback { get; set; }
        public object? State { get; set; }
        public void Execute()
        {
            WaitCallback?.Invoke(State);
        }
    }

    public class ThreadEntity : IDisposable
    {
        private ConcurrentQueue<WorkItem> _workItems;
        private int _waitJobMillisecondsTimeout;
        private CancellationToken _cancellationToken;

        private bool _disposedValue;
        private Thread _thread;
        private string _threadName;
        private object _jobLock = new object();
        private volatile bool _haveJob = false;

        public bool IsDisposed => !_disposedValue;
        public bool IsBusy => !_haveJob;

        public ThreadEntity(ConcurrentQueue<WorkItem> workItems, ThreadPriority threadPriority, int waitJobMillisecondsTimeout, CancellationToken cancellationToken)
        {
            _workItems = workItems;
            _waitJobMillisecondsTimeout = waitJobMillisecondsTimeout;
            _cancellationToken = cancellationToken;

            _thread = new Thread(ThreadBody);
            _thread.Priority = threadPriority;

            _threadName = _thread.Name;

            _thread.Start();
        }

        public void ThreadBody()
        {
            while (!_disposedValue && !_cancellationToken.IsCancellationRequested)
            {
                // 等待通知，等太久就掰掰
                var isTimeout = !SpinWait.SpinUntil(() => _haveJob, _waitJobMillisecondsTimeout);
                _haveJob = false;

                if (isTimeout)
                {
                    Console.WriteLine($"Timeout. _threadName: {_threadName}");

                    this.Dispose();
                    break;
                }

                // 有工作就一直做
                while (_workItems.Count > 0)
                {
                    try
                    {
                        if (_workItems.TryDequeue(out var workItem))
                        {
                            workItem.Execute();
                        }

                        if (_cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        //throw;
                    }
                }
            }

            this.Dispose();
        }

        public void WakeUp()
        {
            //if (!this.IsBusy)
            //{
            //    _haveJob = true;
            //}
            _haveJob = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _thread.Interrupt();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ThreadEntity()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
