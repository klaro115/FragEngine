using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Resources.Management;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace FragEngine3.Resources
{
	public sealed class ResourceManager : IDisposable
	{
		#region Types

		private sealed class QueueHandle
		{
			public QueueHandle(ResourceHandle _resourceHandle, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
			{
				resourceHandle = _resourceHandle;
				assignResourceCallback = _assignResourceCallback;
			}

			public readonly ResourceHandle resourceHandle;
			public readonly ResourceHandle.FuncAssignResourceCallback assignResourceCallback;
		}

		#endregion
		#region Constructors

		public ResourceManager(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(engine), "Engine may not be null!");
			fileLoader = new(this);

			stopwatch = new();
			stopwatch.Start();

			try
			{
				importThread = new Thread(RunAsyncImportThread);
				importThread.Start();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to create and start resource import thread!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				importThread = null!;
			}
		}
		~ResourceManager()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Engine engine;
		public readonly ResourceFileLoader fileLoader;
		private readonly Stopwatch stopwatch;

		private Thread? fileLoaderThread = null;
		private CancellationTokenSource? fileLoaderThreadCancellationSrc = new();
		private Containers.Progress? fileLoaderProgress = null;

		private readonly ConcurrentDictionary<string, ResourceHandle> allResources = new();
		private readonly ConcurrentDictionary<string, ResourceFileHandle> allFiles = new();

		private readonly ConcurrentDictionary<ResourceType, ConcurrentDictionary<string, ResourceHandle>> resourceTypeDict = new();
		private readonly List<QueueHandle> loadQueue = new(32);

		private readonly Thread importThread;
		private readonly CancellationTokenSource importThreadCancellationSrc = new();
		private readonly Containers.Progress importThreadProgress = new("Async resource import", 1);

		private readonly object queueLockObj = new();
		private readonly object resourceLockObj = new();

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		/// <summary>
		/// Gets the total number of all registered and known resources.
		/// </summary>
		public int TotalResourceCount => allResources.Count;
		/// <summary>
		/// Gets the total number of registered and known resource files.
		/// </summary>
		public int TotalFileCount => allFiles.Count;
		/// <summary>
		/// Gets the total number of resources that are currently queued up for loading.
		/// </summary>
		public int QueuedResourceCount { get { lock (queueLockObj) { return loadQueue.Count; } } }
		/// <summary>
		/// Gets a progress object for tracking or visualizing the workload of asynchronous background loading.
		/// </summary>
		public Containers.Progress CurrentQueueProgress => importThreadProgress;

		/// <summary>
		/// Gets a singleton instance of a resource manager, if available. The first resource manager instance that was created is generally assigned as singleton instance.
		/// </summary>
		public static ResourceManager? Instance { get; private set; } = null;

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
			fileLoaderThreadCancellationSrc?.Cancel();

			while (_disposing && importThread.IsAlive || (fileLoaderThread != null && fileLoaderThread.IsAlive))
			{
				Thread.Sleep(1);
			}

			AbortAllImports();
			DisposeAllResources();

			if (_disposing)
			{
				importThreadProgress?.Finish();
			}

			stopwatch.Stop();
		}

		public void DisposeAllResources()
		{
			lock (queueLockObj)
			{
				loadQueue.Clear();
			}

			lock(resourceLockObj)
			{
				var allRes = allResources.ToArray();
				foreach (var kvp in allRes)
				{
					kvp.Value?.Unload();
				}
				allResources.Clear();

				foreach (var resourceDict in resourceTypeDict)
				{
					resourceDict.Value?.Clear();
				}
			}
		}

		public void AbortAllImports()
		{
			// Abort and discard file loader processes:
			fileLoaderThreadCancellationSrc?.Cancel();
			while (fileLoaderThread != null && fileLoaderThread.IsAlive)
			{
				Thread.Sleep(1);
			}
			fileLoaderProgress?.CompleteAllTasks();

			fileLoaderThreadCancellationSrc = null;
			fileLoaderThread = null;
			fileLoaderProgress = null;

			// Purge and reset import queue contents:
			lock (queueLockObj)
			{
				QueueHandle[] queueElements = loadQueue.ToArray();
				loadQueue.Clear();

				foreach (QueueHandle handle in queueElements)
				{
					if (handle.resourceHandle.loadState == ResourceLoadState.Queued)
					{
						handle.resourceHandle.loadState = ResourceLoadState.NotLoaded;
					}
				}
			}
			importThreadProgress.Update(null, 0, 0);
		}

		public bool GatherAllResourceFiles(bool _immediately, out Containers.Progress _outProgress)
		{
			if (fileLoaderThread != null && fileLoaderThread.IsAlive)
			{
				Console.WriteLine("Error! Another file loader operation is currently running! Wait for that to conclude before issueing further calls.");
				_outProgress = new(string.Empty, 0);
				return false;
			}
			fileLoaderThreadCancellationSrc?.Cancel();
			fileLoaderThreadCancellationSrc = new();

			fileLoaderProgress?.CompleteAllTasks();
			fileLoaderProgress = null;

			if (_immediately)
			{
				return fileLoader.GatherAllResourceFiles(out _outProgress);
			}
			else
			{
				try
				{
					fileLoaderThread = new Thread(RunAsyncFileLoaderThread);
					fileLoaderThread.Start();

					do
					{
						Thread.Sleep(2);
					}
					while (fileLoaderProgress == null);

					_outProgress = fileLoaderProgress;
					return true;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error! Failed to create and start file loader thread!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
					fileLoaderThreadCancellationSrc?.Cancel();
					fileLoaderThreadCancellationSrc = null;
					fileLoaderThread = null;
					_outProgress = new(string.Empty, 0);
					return false;
				}
			}
		}

		private void RunAsyncFileLoaderThread()
		{
			if (fileLoader.GatherAllResourceFiles(out fileLoaderProgress, false))
			{
				fileLoaderProgress.CompleteAllTasks();
			}

			fileLoaderThreadCancellationSrc?.Cancel();
			fileLoaderThreadCancellationSrc = null;
			fileLoaderThread = null;
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
						queueElement.resourceHandle.loadState = ResourceLoadState.Loading;
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
					if (LoadImmediately(queueElement.resourceHandle, queueElement.assignResourceCallback))
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

		public bool HasFile(string _fileKey)
		{
			return !string.IsNullOrEmpty(_fileKey) && allFiles.ContainsKey(_fileKey);
		}

		public bool GetFile(string _fileKey, out ResourceFileHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_fileKey))
			{
				Console.WriteLine("Error! Cannot find resource file using null or blank key!");
				_outHandle = ResourceFileHandle.None;
				return false;
			}
			if (!allFiles.TryGetValue(_fileKey, out ResourceFileHandle? handle) || handle == null)
			{
				_outHandle = ResourceFileHandle.None;
				return false;
			}
			_outHandle = handle;
			return true;
		}

		public bool AddFile(ResourceFileHandle _handle)
		{
			if (_handle == null)
			{
				Console.WriteLine("Error! Cannot register null resource!");
				return false;
			}
			if (!_handle.IsValid)
			{
				Console.WriteLine("Error! Cannot register invalid or incomplete resource!");
				return false;
			}
			if (HasFile(_handle.Key))
			{
				Console.WriteLine($"Error! A resource file with key '{_handle.Key}' already exists!");
				return false;
			}

			// Register new file handle:
			lock(resourceLockObj)
			{
				allFiles.TryAdd(_handle.Key, _handle);
			}

			// NOTE: Resource handles created from this file handle must be added seperately after registering the file.
			return true;
		}

		public bool RemoveFile(string _fileKey)
		{
			if (string.IsNullOrEmpty(_fileKey))
			{
				Console.WriteLine("Error! Cannot unregister file using null or blank file key!");
				return false;
			}

			lock(resourceLockObj)
			{
				return allFiles.TryRemove(_fileKey, out _);
			}
		}

		/// <summary>
		/// Try to find the file containing a given resource.<para/>
		/// NOTE: Only resource handles loaded from a file can be found; resources that are procedurally generated, or
		/// downloaded from a network source will not yield any results.
		/// </summary>
		/// <param name="_resourceKey">The key used to identify the resource. Case-sentive, must be non-null.</param>
		/// <param name="_outFileHandle">Outputs a handle to the file containing the resource, or null, if no file was found.</param>
		/// <returns></returns>
		public bool GetFileWithResource(string _resourceKey, out ResourceFileHandle? _outFileHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey))
			{
				Console.WriteLine("Error! Resource key may not be null or blank!");
				_outFileHandle = null;
				return false;
			}

			var fileHandles = from f in allFiles from r in f.Value.resourceInfos where r.resourceKey == _resourceKey select f.Value;

			_outFileHandle = fileHandles.FirstOrDefault();
			return _outFileHandle != null;
		}

		/// <summary>
		/// Get an enumerator for iterating over all resource file handles registered with this resource manager.
		/// </summary>
		/// <param name="_loadedOnly">Whether to only iterate over files that have been fully loaded.</param>
		/// <returns></returns>
		public IEnumerator<ResourceFileHandle> IterateFiles(bool _loadedOnly = false)
		{
			if (IsDisposed) yield break;

			foreach (var kvp in allFiles)
			{
				if (!_loadedOnly || kvp.Value.IsLoaded)
				{
					yield return kvp.Value;
				}
			}
		}

		/// <summary>
		/// Check if a resource is registered.
		/// </summary>
		/// <param name="_resourceKey">The unique identifier for the resource and its handle.</param>
		/// <returns>True if the resource exists and is registered with this resource manager, false otherwise.</returns>
		public bool HasResource(string _resourceKey)
		{
			return !string.IsNullOrEmpty(_resourceKey) && allResources.ContainsKey(_resourceKey);
		}

		/// <summary>
		/// Retrieve a resource handle for accessing and managing a specific resource.
		/// </summary>
		/// <param name="_resourceKey">The unique identifier for the resource and its handle.</param>
		/// <param name="_outHandle">Outputs a resource handle through which the resource may be accessed and loaded.</param>
		/// <returns>True if the resource exists and could be found, false otherwise.</returns>
		public bool GetResource(string _resourceKey, out ResourceHandle _outHandle)
		{
			if (string.IsNullOrEmpty(_resourceKey))
			{
				Console.WriteLine("Error! Cannot find resource using null or blank key!");
				_outHandle = ResourceHandle.None;
				return false;
			}
			if (!allResources.TryGetValue(_resourceKey, out ResourceHandle? handle) || handle == null)
			{
				_outHandle = ResourceHandle.None;
				return false;
			}
			_outHandle = handle;
			return true;
		}

		public bool AddResource(ResourceHandle _handle)
		{
			if (_handle == null)
			{
				Console.WriteLine("Error! Cannot register null resource!");
				return false;
			}
			if (!_handle.IsValid)
			{
				Console.WriteLine("Error! Cannot register invalid or incomplete resource!");
				return false;
			}
			if (HasResource(_handle.Key))
			{
				Console.WriteLine($"Error! A resource with key '{_handle.Key}' already exists!");
				return false;
			}

			// Register new resource handle:
			lock(resourceLockObj)
			{
				// Add to pool of all resources:
				allResources.TryAdd(_handle.Key, _handle);

				// Add to pool of typed resources:
				if (!resourceTypeDict.TryGetValue(_handle.resourceType, out ConcurrentDictionary<string, ResourceHandle>? typeDict))
				{
					typeDict = new ConcurrentDictionary<string, ResourceHandle>();
					resourceTypeDict.TryAdd(_handle.resourceType, typeDict);
				}
				typeDict.TryAdd(_handle.Key, _handle);
			}
			return true;
		}

		public bool RemoveResource(string _resourceKey)
		{
			if (string.IsNullOrEmpty(_resourceKey))
			{
				Console.WriteLine("Error! Cannot unregister resource using null or blank resource key!");
				return false;
			}

			// Abort/Reset the resource's loading process:
			if (GetResource(_resourceKey, out ResourceHandle? handle))
			{
				if (handle.loadState != ResourceLoadState.NotLoaded)
				{
					// Dequeue handle to prevent it from loading:
					if (handle.loadState == ResourceLoadState.Queued)
					{
						lock (queueLockObj)
						{
							loadQueue.RemoveAll(o => o.resourceHandle == handle);
						}
						handle.loadState = ResourceLoadState.NotLoaded;
					}
					// If it's already being loaded, wait for completion, block thread until done:
					else if (handle.loadState == ResourceLoadState.Loading)
					{
						const int timeoutMs = 5000;
						for (int i = 0; i < timeoutMs; ++i)
						{
							Thread.Sleep(1);
							if (handle.loadState != ResourceLoadState.Loading)
							{
								break;
							}
						}
					}
				}

				// Unload resource immediately:
				handle.Unload();
			}

			// Remove resource handle from lookup tables:
			lock (resourceLockObj)
			{
				if (handle != null)
				{
					if (resourceTypeDict.TryGetValue(handle.resourceType, out var typedDict))
					{
						typedDict.TryRemove(_resourceKey, out _);
					}
				}
				else
				{
					foreach (var kvp in resourceTypeDict)
					{
						if (kvp.Value.TryRemove(_resourceKey, out _)) break;
					}
				}

				return allResources.TryRemove(_resourceKey, out _);
			}
		}

		public bool LoadResource(string _resourceKey, bool _loadImmediately, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
		{
			return GetResource(_resourceKey, out ResourceHandle handle) && LoadResource(handle, _loadImmediately, _assignResourceCallback);
		}

		public bool LoadResource(ResourceHandle _handle, bool _loadImmediately, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
		{
			if (_handle == null)
			{
				Console.WriteLine("Error! Cannot load null resource handle!");
				return false;
			}
			if (!_handle.IsValid)
			{
				Console.WriteLine("Error! Cannot load invalid or incomplete resource handle!");
				_handle.loadState = ResourceLoadState.NotLoaded;
				return false;
			}
			if (_handle.IsLoaded)
			{
				return true;
			}



			// TODO: Load dependencies before enqueueing or loading handle!



			bool success = true;

			// Load resource immediately on the main thread:
			if (_loadImmediately)
			{
				// If resource is queued up already, dequeue it first:
				if (_handle.loadState == ResourceLoadState.Queued)
				{
					lock(queueLockObj)
					{
						loadQueue.RemoveAll(o => o.resourceHandle == _handle);
						_handle.loadState = ResourceLoadState.NotLoaded;
					}
				}
				// If resource is currently in the process of being imported, block and wait for it to conclude:
				if (_handle.loadState == ResourceLoadState.Loading)
				{
					const int timeoutMs = 5000;
					for (int i = 0; i < timeoutMs; ++i)
					{
						Thread.Sleep(1);
						if (_handle.loadState != ResourceLoadState.Loading)
						{
							break;
						}
					}
				}
				// Execute immediate loading:
				if (_handle.loadState == ResourceLoadState.NotLoaded)
				{
					success &= LoadImmediately(_handle, _assignResourceCallback);
				}
			}
			// Queue resource up for asynchronous loading by the import thread:
			else
			{
				lock(queueLockObj)
				{
					_handle.loadState = ResourceLoadState.Queued;
					loadQueue.Add(new QueueHandle(_handle, _assignResourceCallback));
				}
			}

			return success;
		}

		/// <summary>
		/// Internal method for loading a resource immediately and on the current thread. It will block until all import processing has concluded.<para/>
		/// NOTE: This method is used both for synchronous (i.e. immediate) and asynchronous (i.e. background) loading of resources.
		/// </summary>
		/// <param name="_handle">A resource handle through which the resource may be identified and its </param>
		/// <param name="_assignResourceCallback">Callback method for assigning the imported resource object to the provided resource handle once ready.</param>
		/// <returns>true if loading of the resource concluded successfully, and the resource object is now ready for use. False otherwise. The resource handle
		/// will be marked as '<see cref="ResourceLoadState.NotLoaded"/>' if any part of the laoding process fails.</returns>
		private bool LoadImmediately(ResourceHandle _handle, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
		{
			if (_handle == null)
			{
				Console.WriteLine("Error! Cannot load null resource handle!");
				return false;
			}
			if (_handle.IsLoaded)
			{
				return true;
			}
			
			// Flag the resource as being processed, to prevent access from other threads before completion:
			_handle.loadState = ResourceLoadState.Loading;

			if (_assignResourceCallback == null)
			{
				Console.WriteLine("Error! Resource assignment callback may not be null!");
				_handle.loadState = ResourceLoadState.NotLoaded;
				return false;
			}

			long startTimestampMs = stopwatch.ElapsedMilliseconds;

			// Call importers to actually load the resource:
			bool success;
			switch (_handle.resourceType)
			{
				case ResourceType.Shader:
					if (success = ShaderResource.CreateShader(_handle, engine.GraphicsSystem.graphicsCore, out ShaderResource? shaderRes))
					{
						_assignResourceCallback(shaderRes);
					}
					break;
				case ResourceType.Material:
					if (success = Material.CreateMaterial(_handle, engine.GraphicsSystem.graphicsCore, out Material? material))
					{
						_assignResourceCallback(material);
					}
					break;
				case ResourceType.Model:
					if ((success = ModelImporter.ImportModelData(_handle, out Graphics.Data.MeshSurfaceData? surfaceData) && surfaceData != null) &&
						(success = ModelImporter.CreateMesh(_handle, engine.GraphicsSystem.graphicsCore, surfaceData!, out Mesh? mesh)))
					{
						_assignResourceCallback(mesh);
					}
					break;
				//...
				default:
					success = false;
					break;
			}

			// Abort and reset load state if import has failed:
			if (!success)
			{
				Console.WriteLine($"Error! Failed to load resource '{_handle}'!");
				lock (resourceLockObj)
				{
					_handle.loadState = ResourceLoadState.NotLoaded;
				}
				return false;
			}

			long endTimestampMs = stopwatch.ElapsedMilliseconds;
			long loadDurationMs = endTimestampMs - startTimestampMs;

			// On success, mark as loaded and return:
			lock (resourceLockObj)
			{
				_handle.loadState = ResourceLoadState.Loaded;
			}

			Console.WriteLine($"* Loaded resource: '{_handle}' ({loadDurationMs}ms)");
			return true;
		}

		/// <summary>
		/// Retrieve a specific resource object.<para/>
		/// NOTE: Use '<see cref="GetResource(string, out ResourceHandle)"/>' and access resources through their handle instead, unless you know exactly what you're doing.
		/// </summary>
		/// <param name="_resourceKey">The unique identifier through which a resource may be identified.</param>
		/// <param name="_assignResourceCallback">Callback method for safely assigning the resource object. This is a callback, to allow assignment through private members
		/// or heavily abstracted channels. The returned resource may be null if not loaded yet, and no loading will be triggered by this method. May not be null.</param>
		/// <returns>True if the resource could be found, false otherwise.</returns>
		public bool GetResourceObject(string _resourceKey, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
		{
			if (_assignResourceCallback == null)
			{
				Console.WriteLine("Error! Resource assignment callback may not be null!");
				return false;
			}
			if (!GetResource(_resourceKey, out ResourceHandle handle))
			{
				return false;
			}
			return _assignResourceCallback(handle.GetResource(false, false));
		}

		/// <summary>
		/// Unload a specific resource.
		/// </summary>
		/// <param name="_resourceKey">The resource key used to identify a resource and its handle.</param>
		/// <param name="_silentFailure">Whether to log errors if the resource wasn't found. If false, no errors are logged, which is great for shutdown operation
		/// where resources might have been removed at a prior time, but you don't want the console clogged with redundant or out-of-date messages.</param>
		/// <returns>True if the resource was known and registered, and couild be unloaded successfully. False if it wasn't registered.</returns>
		public bool UnloadResource(string _resourceKey, bool _silentFailure = false)
		{
			if (!GetResource(_resourceKey, out ResourceHandle handle))
			{
				if (!_silentFailure) Console.WriteLine($"Error! Cannot unload resource with unregistered/unknown key '{_resourceKey ?? "NULL"}'!");
				return false;
			}

			// If the resource object is available, terminate it and reset handle's load state:
			Resource? resourceObj = handle.GetResource(false, false);
			if (resourceObj != null && !resourceObj.IsDisposed)
			{
				resourceObj.Dispose();
				handle.loadState = ResourceLoadState.NotLoaded;
			}
			return true;
		}

		/// <summary>
		/// Get an enumerator for iterating over all resource handles registered with this resource manager.
		/// </summary>
		/// <param name="_loadedOnly">Whether to only iterate over resources that have been fully loaded.</param>
		/// <returns></returns>
		public IEnumerator<ResourceHandle> IterateResources(bool _loadedOnly)
		{
			if (IsDisposed) yield break;

			foreach (var kvp in allResources)
			{
				if (!_loadedOnly || kvp.Value.IsLoaded)
				{
					yield return kvp.Value;
				}
			}
		}

		#endregion
	}
}
