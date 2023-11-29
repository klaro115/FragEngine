
namespace FragEngine3.Resources.Management
{
	public sealed class ResourceImporter : IDisposable
	{
		#region Types

		private sealed class QueueHandle(ResourceHandle _resourceHandle, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
		{
			public readonly ResourceHandle resourceHandle = _resourceHandle;
			public readonly ResourceHandle.FuncAssignResourceCallback assignResourceCallback = _assignResourceCallback;
		}

		#endregion
		#region Constructors

		internal ResourceImporter(ResourceManager _resourceManager, ResourceManager.FuncLoadImmediately _funcLoadImmediately)
		{
			resourceManager = _resourceManager ?? throw new ArgumentNullException(nameof(_resourceManager), "Resource manager may not be null!");
			funcLoadImmediately = _funcLoadImmediately ?? throw new ArgumentNullException(nameof(_funcLoadImmediately), "Callback for immediate resource loading may not be null!");

			try
			{
				importThread = new Thread(RunAsyncImportThread);
				importThread.Start();
			}
			catch (Exception ex)
			{
				resourceManager.engine.Logger.LogException("Failed to create and start resource import thread!", ex);
				importThread = null!;
			}
		}

		~ResourceImporter()
		{
			Dispose(false);
		}

		#endregion
		#region Fields

		public readonly ResourceManager resourceManager;
		private readonly ResourceManager.FuncLoadImmediately funcLoadImmediately;

		private readonly List<QueueHandle> loadQueue = new(32);

		private readonly Thread importThread;
		private readonly CancellationTokenSource importThreadCancellationSrc = new();
		public readonly Containers.Progress importThreadProgress = new("Async resource import", 1);

		internal readonly object queueLockObj = new();

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		public int QueuedResourceCount { get { lock (queueLockObj) { return loadQueue.Count; } } }

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _disposing)
		{
			IsDisposed = true;

			importThreadCancellationSrc?.Cancel();

			while (_disposing && importThread.IsAlive)
			{
				Thread.Sleep(1);
			}

			AbortAllImports();

			if (_disposing)
			{
				importThreadProgress?.Finish();
			}
		}

		public void AbortAllImports()
		{
			// Purge and reset import queue contents:
			lock (queueLockObj)
			{
				QueueHandle[] queueElements = [.. loadQueue];
				loadQueue.Clear();

				foreach (QueueHandle handle in queueElements)
				{
					if (handle.resourceHandle.LoadState == ResourceLoadState.Queued)
					{
						handle.resourceHandle.LoadState = ResourceLoadState.NotLoaded;
					}
				}
			}

			importThreadProgress.Update(null, 0, 0);
		}

		public bool RemoveResource(ResourceHandle _handle)
		{
			if (_handle == null) return false;

			lock (queueLockObj)
			{
				loadQueue.RemoveAll(o => o.resourceHandle == _handle);
			}

			if (_handle.LoadState != ResourceLoadState.Loaded)
			{
				_handle.LoadState = ResourceLoadState.NotLoaded;
			}
			return true;
		}

		internal bool EnqueueResource(ResourceHandle _handle, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
		{
			if (_handle == null || _assignResourceCallback == null) return false;

			if (_handle.LoadState == ResourceLoadState.Loaded) return true;

			lock (queueLockObj)
			{
				_handle.LoadState = ResourceLoadState.Queued;
				loadQueue.Add(new QueueHandle(_handle, _assignResourceCallback));
			}
			return true;
		}

		/// <summary>
		/// Thread function for asynchronous resource loading. Resource handles are dequeued from the load queue and passed on to the appropriate importers.
		/// </summary>
		private void RunAsyncImportThread()
		{
			while (!IsDisposed && !importThreadCancellationSrc.IsCancellationRequested)
			{
				bool queueIsEmpty;
				QueueHandle? queueElement = null;

				// Dequeue first element in the queue:
				lock (queueLockObj)
				{
					queueIsEmpty = loadQueue.Count == 0;
					if (importThreadProgress.taskCount != loadQueue.Count)
					{
						importThreadProgress.Update(null, importThreadProgress.tasksDone, loadQueue.Count);
					}
					if (!queueIsEmpty)
					{
						queueElement = loadQueue.First();
						queueElement.resourceHandle.LoadState = ResourceLoadState.Loading;
						loadQueue.RemoveAt(0);
					}
				}

				// If queue was empty, idle until more work shows up:
				if (queueIsEmpty)
				{
					if (importThreadProgress.tasksDone != 0) importThreadProgress.Update(null, 0, 0);
					Thread.Sleep(1);
				}
				// If a resource could be dequeued, load it immediately within this thread:
				else if (queueElement != null)
				{
					if (funcLoadImmediately(queueElement.resourceHandle, queueElement.assignResourceCallback))
					{
						importThreadProgress.Increment();
					}
					else
					{
						importThreadProgress.errorCount++;
					}
				}
			}

			importThreadCancellationSrc.Cancel();
		}

		#endregion
	}
}
