using FragEngine3.Containers;
using FragEngine3.Resources.Data;
using FragEngine3.Utility;
using System.Diagnostics;

namespace FragEngine3.Resources.Management
{
	public sealed class ResourceFileGatherer : IDisposable
	{
		#region Types

		private sealed class ResourceLibrary(string _path, string _name, ResourceSource _source)
		{
			public readonly string path = _path ?? string.Empty;
			public readonly string name = _name ?? string.Empty;
			public readonly ResourceSource source = _source;
		}

		#endregion
		#region Constructors

		public ResourceFileGatherer(ResourceManager _resourceManager)
		{
			resourceManager = _resourceManager ?? throw new ArgumentNullException(nameof(_resourceManager), "Resource manager may not be null!");

			applicationPath = Path.GetFullPath(Environment.CurrentDirectory);
			rootResourcePath = Path.Combine(applicationPath, ResourceConstants.RESOURCE_ROOT_DIR_REL_PATH);
			coreResourcePath = Path.Combine(rootResourcePath, ResourceConstants.RESOURCE_CORE_DIR_REL_PATH);
			modsResourcePath = Path.Combine(rootResourcePath, ResourceConstants.RESOURCE_MODS_DIR_REL_PATH);
		}

		~ResourceFileGatherer()
		{
			Dispose(false);
		}

		#endregion
		#region Fields

		public readonly ResourceManager resourceManager;
		private readonly Stopwatch stopwatch = new();

		public readonly string applicationPath = string.Empty;
		public readonly string rootResourcePath = string.Empty;
		private readonly string coreResourcePath = string.Empty;
		private readonly string modsResourcePath = string.Empty;

		private CancellationTokenSource? gatherThreadCancellationSrc = null;
		private Thread? gatherThread = null;
		private Progress? gatherProgress = null;

		private readonly List<ResourceLibrary> libraries = [];

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		public bool IsRunning => !IsDisposed && gatherProgress != null && !gatherProgress.IsFinished;

		private bool IsCancelRequested => gatherThreadCancellationSrc == null || gatherThreadCancellationSrc.IsCancellationRequested;

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

			AbortGathering();

			if (gatherThreadCancellationSrc != null)
			{
				gatherThreadCancellationSrc.Cancel();
				gatherThreadCancellationSrc.Dispose();
			}

			if (_disposing)
			{
				libraries.Clear();

				gatherThreadCancellationSrc = null;
				gatherThread = null;
				gatherProgress = null;
			}

			stopwatch.Stop();
		}

		public void AbortGathering()
		{
			gatherThreadCancellationSrc?.Cancel();

			if (gatherThread != null && gatherThread.IsAlive)
			{
				const int timeout = 100;
				int timer = 0;
				while (timer++ < timeout && gatherThread.IsAlive)
				{
					Thread.Sleep(1);
				}
			}

			gatherThread = null;
			gatherThreadCancellationSrc?.Dispose();
			gatherThreadCancellationSrc = null;

			gatherProgress?.CompleteAllTasks();
			gatherProgress?.Finish();
		}

		public bool GatherAllResources(bool _gatherImmediately, out Progress _outProgress)
		{
			gatherProgress = new("Starting resource gathering...", 1);
			_outProgress = gatherProgress;

			if (_gatherImmediately)
			{
				RunFileGatherThread();
			}
			else
			{
				gatherThreadCancellationSrc = new();

				try
				{
					gatherThread = new(RunFileGatherThread);
					gatherThread.Start();
				}
				catch (Exception ex)
				{
					gatherThreadCancellationSrc.Cancel();
					gatherThreadCancellationSrc.Dispose();

					resourceManager.engine.Logger.LogException("Failed to start resource file gathering thread!", ex);
					_outProgress.Finish();
					return false;
				}
			}

			return true;
		}

		private void RunFileGatherThread()
		{
			stopwatch.Restart();

			libraries.Clear();

			if (!IsCancelRequested && !VerifyResourceDirectories())
			{
				resourceManager.engine.Logger.LogError("Failed to verify or create main resource directories!");
				goto exit;
			}

			if (!IsCancelRequested && !GatherAllLibraries())
			{
				resourceManager.engine.Logger.LogError("Failed to gather resource libraries!");
				goto exit;
			}

			if (!IsCancelRequested && !GatherResourceFiles())
			{
				resourceManager.engine.Logger.LogError("Failed to gather resource files!");
				goto exit;
			}

			gatherProgress?.CompleteAllTasks();

		exit:
			// Terminate progress:
			gatherProgress?.CompleteAllTasks();
			gatherProgress?.Finish();

			// Purge thread data as it exits:
			if (gatherThreadCancellationSrc != null)
			{
				gatherThreadCancellationSrc.Cancel();
				gatherThreadCancellationSrc.Dispose();
			}
			gatherThreadCancellationSrc = null;
			gatherThread = null;
		}

		private bool VerifyResourceDirectories()
		{
			long startTimeMs = stopwatch.ElapsedMilliseconds;
			int progressTaskCount = 3 + ResourceConstants.coreResourceLibraries.Length;
			gatherProgress?.Update("Verifying resource directories", 0, progressTaskCount);

			resourceManager.engine.Logger.LogMessage("+ Verifying resource directories...");

			try
			{
				// Verify root & application directories:
				if (!Directory.Exists(rootResourcePath))
				{
					resourceManager.engine.Logger.LogMessage("  - Creating resource root directory.");
					Directory.CreateDirectory(rootResourcePath);
				}
				gatherProgress?.Increment();
				if (!Directory.Exists(coreResourcePath))
				{
					resourceManager.engine.Logger.LogMessage("  - Creating core resource directory.");
					Directory.CreateDirectory(coreResourcePath);
				}
				gatherProgress?.Increment();

				// Verify core libraries:
				foreach (string coreLibName in ResourceConstants.coreResourceLibraries)
				{
					string coreLibPath = Path.Combine(coreResourcePath, coreLibName);
					if (!Directory.Exists(coreLibPath))
					{
						resourceManager.engine.Logger.LogMessage($"    * Creating core library '{coreLibName}'.");
						Directory.CreateDirectory(coreLibPath);
					}
					gatherProgress?.Increment();
				}

				// Verify mod directory:
				if (!Directory.Exists(modsResourcePath))
				{
					resourceManager.engine.Logger.LogMessage("  - Creating mod resource directory.");
					Directory.CreateDirectory(modsResourcePath);
				}
				gatherProgress?.Increment();

				long endTimeMs = stopwatch.ElapsedMilliseconds;
				long durationMs = endTimeMs - startTimeMs;
				resourceManager.engine.Logger.LogMessage($"+ All resource directories verified. ({durationMs} ms)");

				return true;
			}
			catch (IOException ex)
			{
				resourceManager.engine.Logger.LogException("An IO exception was caught while creating missing resource directory!", ex);
			}
			catch (UnauthorizedAccessException ex)
			{
				resourceManager.engine.Logger.LogException("An authorization exception was caught while creating missing resource directory! Make sure you have permission to write to the application's root folder!", ex);
			}
			catch (Exception ex)
			{
				resourceManager.engine.Logger.LogException("An exception was caught while creating missing resource directories!", ex);
			}

			gatherProgress?.UpdateTitle("Resource directory verification failed");
			return false;
		}

		private bool GatherAllLibraries()
		{
			long startTimeMs = stopwatch.ElapsedMilliseconds;
			int progressTaskCount = 2 + ResourceConstants.coreResourceLibraries.Length;
			gatherProgress?.Update("+ Gathering all resource libraries", 0, progressTaskCount);

			resourceManager.engine.Logger.LogMessage("+ Gathering all resource libraries...");

			// Core libs:
			foreach (string coreLibName in ResourceConstants.coreResourceLibraries)
			{
				string coreLibPath = Path.Combine(coreResourcePath, coreLibName);
				libraries.Add(new(coreLibPath, coreLibName, ResourceSource.Core));

				resourceManager.engine.Logger.LogMessage($"  - Found core library '{coreLibName}'.");
				gatherProgress?.Increment();
			}

			// Application libs:
			try
			{
				IEnumerable<string> applicationLibDirs = Directory.EnumerateDirectories(rootResourcePath);
				IEnumerator<string> e = applicationLibDirs.GetEnumerator();
				while (!IsCancelRequested && e.MoveNext())
				{
					if (string.Compare(e.Current, coreResourcePath, StringComparison.InvariantCultureIgnoreCase) != 0 &&
						string.Compare(e.Current, modsResourcePath, StringComparison.InvariantCultureIgnoreCase) != 0)
					{
						string libName = PathTools.GetLastPartName(e.Current);
						libraries.Add(new(e.Current, libName, ResourceSource.Application));

						resourceManager.engine.Logger.LogMessage($"  - Found application library '{libName}'.");
					}
				}
			}
			catch (Exception ex)
			{
				resourceManager.engine.Logger.LogException("Failed to gather application resource libraries!", ex);
				return false;
			}
			gatherProgress?.Increment();

			// Mod libs:
			try
			{
				IEnumerable<string> modLibDirs = Directory.EnumerateDirectories(modsResourcePath);
				IEnumerator<string> e = modLibDirs.GetEnumerator();
				while (!IsCancelRequested && e.MoveNext())
				{
					string libName = PathTools.GetLastPartName(e.Current);
					libraries.Add(new(e.Current, libName, ResourceSource.Mod));

					resourceManager.engine.Logger.LogMessage($"  - Found mod library '{libName}'.");
				}
			}
			catch (Exception ex)
			{
				resourceManager.engine.Logger.LogException("Failed to gather mod resource libraries!", ex);
				return false;
			}
			gatherProgress?.Increment();

			long endTimeMs = stopwatch.ElapsedMilliseconds;
			long durationMs = endTimeMs - startTimeMs;
			resourceManager.engine.Logger.LogMessage($"+ All resource libraries gathered. ({durationMs} ms)");
			return true;
		}

		private bool GatherResourceFiles()
		{
			const string RES_FILE_SEARCH_PATTERN = "*" + ResourceConstants.FILE_EXT_METADATA;

			long startTimeMs = stopwatch.ElapsedMilliseconds;
			gatherProgress?.Update("+ Gathering all resource files", 0, libraries.Count);

			resourceManager.engine.Logger.LogMessage("+ Gathering all resource files...");

			int totalFileCount = 0;
			IEnumerator<ResourceLibrary> libraryEnumerator = libraries.GetEnumerator();
			while (!IsCancelRequested && libraryEnumerator.MoveNext())
			{
				ResourceLibrary lib = libraryEnumerator.Current;
				int libFileCount = 0;
				int libFileFailed = 0;

				try
				{
					// Get all resource descriptor files in this library and create file handles for them:
					IEnumerable<string> resFilePaths = Directory.EnumerateFiles(lib.path, RES_FILE_SEARCH_PATTERN, SearchOption.AllDirectories);
					IEnumerator<string> resourceEnumerator = resFilePaths.GetEnumerator();
					while (!IsCancelRequested && resourceEnumerator.MoveNext())
					{
						if (ResourceFileData.DeserializeFromFile(resourceEnumerator.Current, out ResourceFileData fileData) && resourceManager.AddFile(new(fileData, lib.source, resourceEnumerator.Current)))
						{
							libFileCount++;
						}
						else
						{
							libFileFailed++;
						}
					}
				}
				catch (Exception ex)
				{
					resourceManager.engine.Logger.LogException($"Failed to gather resource files from library '{lib.path}'!", ex);
					return false;
				}
				
				resourceManager.engine.Logger.LogMessage($"  - Loaded library '{lib.name}' - Resources: {libFileCount}, Failed: {libFileFailed}");
				gatherProgress?.Increment();
			}

			long endTimeMs = stopwatch.ElapsedMilliseconds;
			long durationMs = endTimeMs - startTimeMs;
			resourceManager.engine.Logger.LogMessage($"+ All resource files gathered. ({totalFileCount} files in {libraries.Count} libraries) ({durationMs} ms)");
			return true;
		}

		#endregion
	}
}
