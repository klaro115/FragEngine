using System.Collections.Concurrent;
using System.Diagnostics;
using FragAssetFormats.Shaders;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Import;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Resources.Management;

namespace FragEngine3.Resources;

public sealed class ResourceManager : IEngineSystem
{
	#region Types

	internal delegate bool FuncLoadImmediately(ResourceHandle _handle, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback);

	#endregion
	#region Constructors

	public ResourceManager(Engine _engine)
	{
		engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
		fileGatherer = new(this);
		importer = new(this, LoadImmediately);

		modelImporter = new(this, engine.GraphicsSystem.graphicsCore);
		shaderImporter = new(engine.GraphicsSystem.graphicsCore);
		//...

		stopwatch = new();
		stopwatch.Start();
	}
	~ResourceManager()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly Engine engine;
	public readonly ResourceFileGatherer fileGatherer;
	private readonly ResourceImporter importer;

	public readonly ModelImporter modelImporter;
	public readonly ShaderImporter shaderImporter;
	//...

	private readonly Stopwatch stopwatch;

	private readonly ConcurrentDictionary<string, ResourceHandle> allResources = new();
	private readonly ConcurrentDictionary<string, ResourceFileHandle> allFiles = new();

	private readonly ConcurrentDictionary<ResourceType, ConcurrentDictionary<string, ResourceHandle>> resourceTypeDict = new();

	private readonly object resourceLockObj = new();

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public Engine Engine => engine;

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
	public int QueuedResourceCount => importer.QueuedResourceCount;
	/// <summary>
	/// Gets a progress object for tracking or visualizing the workload of asynchronous background loading.
	/// </summary>
	public Containers.Progress CurrentQueueProgress => importer.importThreadProgress;

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
	private void Dispose(bool _)
	{
		IsDisposed = true;

		AbortAllImports();
		
		importer.Dispose();
		fileGatherer.Dispose();

		modelImporter.Dispose();
		shaderImporter.Dispose();
		//...

		DisposeAllResources();

		stopwatch.Stop();
	}

	public void DisposeAllResources()
	{
		importer.AbortAllImports();

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
		fileGatherer.AbortGathering();
		importer.AbortAllImports();
	}

	public bool HasFile(string _fileKey)
	{
		return !string.IsNullOrEmpty(_fileKey) && allFiles.ContainsKey(_fileKey);
	}

	public bool GetFile(string _fileKey, out ResourceFileHandle _outHandle)
	{
		if (string.IsNullOrEmpty(_fileKey))
		{
			engine.Logger.LogError("Cannot find resource file using null or blank key!");
			_outHandle = ResourceFileHandle.None;
			return false;
		}
		if (!allFiles.TryGetValue(_fileKey, out ResourceFileHandle? handle) || handle is null)
		{
			_outHandle = ResourceFileHandle.None;
			return false;
		}
		_outHandle = handle;
		return true;
	}

	public bool AddFile(ResourceFileHandle _handle)
	{
		if (_handle is null)
		{
			engine.Logger.LogError("Cannot register null resource!");
			return false;
		}
		if (!_handle.IsValid)
		{
			engine.Logger.LogError("Cannot register invalid or incomplete resource!");
			return false;
		}
		if (HasFile(_handle.resourceFilePath))
		{
			engine.Logger.LogError($"A resource file with key '{_handle.resourceFilePath}' already exists!");
			return false;
		}

		// Register new file handle:
		lock(resourceLockObj)
		{
			allFiles.TryAdd(_handle.resourceFilePath, _handle);
		}

		// NOTE: Resource handles created from this file handle must be added seperately after registering the file.
		return true;
	}

	public bool RemoveFile(string _fileKey)
	{
		if (string.IsNullOrEmpty(_fileKey))
		{
			engine.Logger.LogError("Cannot unregister file using null or blank file key!");
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
	/// <param name="_outFileHandle">Outputs a handle to the file containing the resource, or '<see cref="ResourceFileHandle.None"/>', if no file was found.</param>
	/// <returns></returns>
	public bool GetFileWithResource(string _resourceKey, out ResourceFileHandle _outFileHandle)
	{
		if (string.IsNullOrEmpty(_resourceKey))
		{
			engine.Logger.LogError("Resource key may not be null or blank!");
			_outFileHandle = ResourceFileHandle.None;
			return false;
		}

		var fileHandles = from f in allFiles from r in f.Value.resources where string.CompareOrdinal(r, _resourceKey) == 0 select f.Value;

		_outFileHandle = fileHandles.FirstOrDefault() ?? ResourceFileHandle.None;
		return _outFileHandle is not null;
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
			engine.Logger.LogError("Cannot find resource using null or blank key!");
			_outHandle = ResourceHandle.None;
			return false;
		}
		if (!allResources.TryGetValue(_resourceKey, out ResourceHandle? handle) || handle is null)
		{
			_outHandle = ResourceHandle.None;
			return false;
		}
		_outHandle = handle;
		return true;
	}

	/// <summary>
	/// Retrieve a resource handle for accessing and managing a specific resource, then immediately initiates loading if the resource wasn't already loaded.
	/// </summary>
	/// <param name="_resourceKey">The unique identifier for the resource and its handle.</param>
	/// <param name="_loadImmediately">Whether to load the resource immediately on the current thread, if it wasn't loaded already. If false, it will be
	/// queued up for asynchronous background loading instead.</param>
	/// <param name="_outHandle">Outputs a resource handle through which the resource may be accessed and managed.</param>
	/// <returns>True if the resource exists and could be found and loading was initiated, false if it wasn't found or if loading failed.</returns>
	public bool GetAndLoadResource(string _resourceKey, bool _loadImmediately, out ResourceHandle _outHandle)
	{
		return GetResource(_resourceKey, out _outHandle) && _outHandle.Load(_loadImmediately);
	}

	/// <summary>
	/// Registers a new resource, identified by its resource handle, with this resource manager.
	/// </summary>
	/// <param name="_handle">A handle identifying and describing the new resource. Must be non-null
	/// and valid, a resource may not be registered twice.</param>
	/// <returns>True if the given resource handle is valid and was registered successfully, false
	/// otherwise or if another handle with the same resource key already exists.</returns>
	public bool AddResource(ResourceHandle _handle)
	{
		if (_handle is null)
		{
			engine.Logger.LogError("Cannot register null resource!");
			return false;
		}
		if (!_handle.IsValid)
		{
			engine.Logger.LogError("Cannot register invalid or incomplete resource!");
			return false;
		}
		if (HasResource(_handle.resourceKey))
		{
			engine.Logger.LogError($"A resource with key '{_handle.resourceKey}' already exists!");
			return false;
		}

		// Register new resource handle:
		lock(resourceLockObj)
		{
			// Add to pool of all resources:
			allResources.TryAdd(_handle.resourceKey, _handle);

			// Add to pool of typed resources:
			if (!resourceTypeDict.TryGetValue(_handle.resourceType, out ConcurrentDictionary<string, ResourceHandle>? typeDict))
			{
				typeDict = new ConcurrentDictionary<string, ResourceHandle>();
				resourceTypeDict.TryAdd(_handle.resourceType, typeDict);
			}
			typeDict.TryAdd(_handle.resourceKey, _handle);
		}
		return true;
	}

	/// <summary>
	/// Removes a resource from this manager. If the resource was loaded, all its contents and data
	/// are unloaded and disposed first.<para/>
	/// WARNING: Removing a resource that is still in use can lead to unpredictable behaviour. Make
	/// sure there are no lingering references to this resource in any important systems before calling
	/// this method.
	/// </summary>
	/// <param name="_resourceKey">A key to the resource handle we wish to remove, may not be null.</param>
	/// <returns>True if a resource of that key exists and was removed, false otherwise.</returns>
	public bool RemoveResource(string _resourceKey)
	{
		if (string.IsNullOrEmpty(_resourceKey))
		{
			engine.Logger.LogError("Cannot unregister resource using null or blank resource key!");
			return false;
		}

		// Abort/Reset the resource's loading process:
		if (GetResource(_resourceKey, out ResourceHandle? handle))
		{
			if (handle.LoadState != ResourceLoadState.NotLoaded)
			{
				// Dequeue handle to prevent it from loading:
				if (handle.LoadState == ResourceLoadState.Queued)
				{
					importer.RemoveResource(handle);
				}
				// If it's already being loaded, wait for completion, block thread until done:
				else if (handle.LoadState == ResourceLoadState.Loading)
				{
					const int timeoutMs = 5000;
					for (int i = 0; i < timeoutMs; ++i)
					{
						Thread.Sleep(1);
						if (handle.LoadState != ResourceLoadState.Loading)
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
			if (handle is not null)
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

	internal bool LoadResource(string _resourceKey, bool _loadImmediately, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
	{
		return GetResource(_resourceKey, out ResourceHandle handle) && LoadResource(handle, _loadImmediately, _assignResourceCallback);
	}

	internal bool LoadResource(ResourceHandle _handle, bool _loadImmediately, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
	{
		if (_handle is null)
		{
			engine.Logger.LogError("Cannot load null resource handle!");
			return false;
		}
		if (!_handle.IsValid)
		{
			engine.Logger.LogError($"Cannot load invalid or incomplete resource handle '{_handle}'!");
			_handle.LoadState = ResourceLoadState.NotLoaded;
			return false;
		}
		if (_handle.IsLoaded)
		{
			return true;
		}

		// DEPENDENCIES:

		// If the resource has dependencies, queue those up first:
		if (_handle.DependencyCount != 0)
		{
			// Recursively load all dependencies, if those haven't been loaded yet:
			int failureCount = 0;
			foreach (string dependencyKey in _handle.dependencies!)
			{
				if (GetResource(dependencyKey, out ResourceHandle dependencyHandle) && dependencyHandle.IsValid && !dependencyHandle.IsLoaded)
				{
					if (!dependencyHandle.Load(_loadImmediately))
					{
						failureCount++;
					}
				}
			}
			if (failureCount != 0)
			{
				engine.Logger.LogError($"Failed to load {failureCount}/{_handle.DependencyCount} dependencies for resource '{_handle}'!");
			}
		}

		// LOAD PROCESS:

		bool result = true;

		// If requested, load resource immediately on the main thread:
		if (_loadImmediately)
		{
			// If resource is queued up already, dequeue it first:
			if (_handle.LoadState == ResourceLoadState.Queued)
			{
				importer.RemoveResource(_handle);
			}
			// If resource is currently in the process of being imported, block and wait for it to conclude:
			if (_handle.LoadState == ResourceLoadState.Loading)
			{
				const int timeoutMs = 2500;
				for (int i = 0; i < timeoutMs; ++i)
				{
					Thread.Sleep(1);
					if (_handle.LoadState != ResourceLoadState.Loading)
					{
						break;
					}
				}
			}
			// Execute immediate loading:
			if (_handle.LoadState == ResourceLoadState.NotLoaded)
			{
				result &= LoadImmediately(_handle, _assignResourceCallback);
			}
		}
		// Queue resource up for asynchronous loading by the import thread:
		else
		{
			result &= importer.EnqueueResource(_handle, _assignResourceCallback);
		}
		return result;
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
		if (_handle is null)
		{
			engine.Logger.LogError("Cannot load null resource handle!");
			return false;
		}
		if (_handle.IsLoaded)
		{
			return true;
		}
		
		// Flag the resource as being processed, to prevent access from other threads before completion:
		_handle.LoadState = ResourceLoadState.Loading;

		if (_assignResourceCallback is null)
		{
			engine.Logger.LogError("Resource assignment callback may not be null!");
			_handle.LoadState = ResourceLoadState.NotLoaded;
			return false;
		}

		long startTimestampMs = stopwatch.ElapsedMilliseconds;

		// Call importers to actually load the resource:
		bool success;
		switch (_handle.resourceType)
		{
			case ResourceType.Shader:
				if ((success = shaderImporter.ImportShaderData(_handle, out ShaderData? shaderData) && shaderData is not null) &&
					(success = shaderImporter.CreateShader(_handle.resourceKey, shaderData!, out ShaderResource? shaderRes)))
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
				if ((success = modelImporter.ImportModelData(_handle, out MeshSurfaceData? surfaceData) && surfaceData is not null) &&
					(success = modelImporter.CreateMesh(_handle, surfaceData!, out Mesh? mesh)))
				{
					_assignResourceCallback(mesh);
				}
				break;
			case ResourceType.Texture:
				if ((success = ImageImporter.ImportImageData(this, _handle, out RawImageData? rawImageData) && rawImageData is not null) &&
					(success = TextureResource.CreateTexture(_handle, engine.GraphicsSystem.graphicsCore, rawImageData!, out TextureResource? texture)))
				{
					_assignResourceCallback(texture);
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
			engine.Logger.LogError($"Failed to load resource \"{_handle}\"!");
			lock (resourceLockObj)
			{
				_handle.LoadState = ResourceLoadState.NotLoaded;
			}
			return false;
		}

		long endTimestampMs = stopwatch.ElapsedMilliseconds;
		long loadDurationMs = endTimestampMs - startTimestampMs;

		// On success, mark as loaded and return:
		lock (resourceLockObj)
		{
			_handle.LoadState = ResourceLoadState.Loaded;
		}

		engine.Logger.LogMessage($" * Loaded resource: \"{_handle}\" ({loadDurationMs}ms)");
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
	internal bool GetResourceObject(string _resourceKey, ResourceHandle.FuncAssignResourceCallback _assignResourceCallback)
	{
		if (_assignResourceCallback is null)
		{
			engine.Logger.LogError("Resource assignment callback may not be null!");
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
			if (!_silentFailure) engine.Logger.LogError($"Cannot unload resource with unregistered/unknown key '{_resourceKey ?? "NULL"}'!");
			return false;
		}

		// If the resource object is available, terminate it and reset handle's load state:
		Resource? resourceObj = handle.GetResource(false, false);
		if (resourceObj is not null && !resourceObj.IsDisposed)
		{
			resourceObj.Dispose();
		}
		handle.LoadState = ResourceLoadState.NotLoaded;
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
