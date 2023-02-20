using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyTask
{
    public class MyTaskSchedulerExt : TaskScheduler, IDisposable
    {
        private int _semCount = 0;
        private int _semMaxCount = 10;
        private int _runCount = 0;

        private int _coreThreadCount = 0;
        private int _maxThreadCount = 0;
        private int _auxiliaryThreadTimeOut = 2000; //辅助线程释放时间
        private int _activeThreadCount = 0;
        private System.Timers.Timer _timer;
        private object _lockCreateTimer = new object();
        private bool _run = true;


        private Semaphore _sem = null;
        private ConcurrentQueue<Task> _tasks = new ConcurrentQueue<Task>();

        /// <summary>
        /// 活跃线程数
        /// </summary>
        public int ActiveThreadCount
        {
            get { return _activeThreadCount; }
        }

        /// <summary>
        /// 核心线程数
        /// </summary>
        public int CoreThreadCount
        {
            get { return _coreThreadCount; }
        }

        /// <summary>
        /// 最大线程数
        /// </summary>
        public int MaxThreadCount
        {
            get { return _maxThreadCount; }
        }

        public MyTaskSchedulerExt(int coreThreadCount = 10, int maxThreadCount = 20)
        {
            _sem = new Semaphore(0, _semMaxCount);
            _maxThreadCount = maxThreadCount;
            CreateCoreThreads(coreThreadCount);
        }
        public void CancelAll()
        {
            Task tempTask;
            while (_tasks.TryDequeue(out tempTask)) 
            {
                Interlocked.Decrement(ref _runCount);
            }
        }

        private void CreateCoreThreads(int coreThreadCount)
        {
            for (int i = 0; i < _coreThreadCount; i++)
            {
                Interlocked.Increment(ref _activeThreadCount);
                Thread thread = null;


                thread = new Thread(new ThreadStart(() =>
                {
                    Task task;
                    while (_run)
                    {
                        if (_tasks.TryDequeue(out task))
                        {
                            TryExecuteTask(task);
                            Interlocked.Decrement(ref _runCount);
                        }
                        else
                        {
                            _sem.WaitOne();
                            Interlocked.Decrement(ref _semCount);
                        }
                    }

                    Interlocked.Decrement(ref _activeThreadCount);
                    if (_activeThreadCount == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                    }

                }));
                thread.IsBackground = true;
                thread.Start();
            }
        }

        public void Dispose()
        {
            _run = false;
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }

            while (_activeThreadCount > 0)
            {
                _sem.Release();
                Interlocked.Increment(ref _semCount);
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _tasks;
        }

        protected override void QueueTask(Task task)
        {
            _tasks.Enqueue(task);
            while (_semCount >= _semMaxCount)
            {
                Thread.Sleep(10);
            }
            _sem.Release();

            Interlocked.Increment(ref _semCount);
            Interlocked.Increment(ref _runCount);

            if (_activeThreadCount < _maxThreadCount && _activeThreadCount < _runCount)
            {
                CreateThread();
            }

        }

        private void CreateThread()
        {
            Interlocked.Increment(ref _activeThreadCount);
            Thread thread = null;
            thread = new Thread(new ThreadStart(() =>
            {
                Task task;
                DateTime dt = DateTime.Now;
                while (_run && DateTime.Now.Subtract(dt).TotalMilliseconds < _auxiliaryThreadTimeOut)
                {
                    if (_tasks.TryDequeue(out task))
                    {
                        TryExecuteTask(task);
                        Interlocked.Decrement(ref _runCount);
                        dt = DateTime.Now;
                    }
                    else
                    {
                        _sem.WaitOne(_auxiliaryThreadTimeOut);
                        Interlocked.Decrement(ref _semCount);
                    }
                }

                Interlocked.Decrement(ref _activeThreadCount);

                if (_activeThreadCount == _coreThreadCount)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

            }));
            thread.IsBackground = true;
            thread.Start();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }
    }
}
