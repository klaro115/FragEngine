﻿using FragEngine3.Containers;
using FragEngine3.Utility;

namespace FragEngine3.Resources.Management
{
	public sealed class ResourceFileLoader
	{
		#region Types

		private sealed class LibraryDir
		{
			public LibraryDir(string _path, string _name, ResourceSource _source)
			{
				path = _path;
				name = _name;
				source = _source;
			}

			public readonly string path;
			public readonly string name;
			public readonly ResourceSource source;
		}

		#endregion
		#region Constructors

		public ResourceFileLoader(ResourceManager _resourceManager)
		{
			resourceManager = _resourceManager ?? throw new ArgumentNullException(nameof(_resourceManager), "Resource manager ay not be null!");

			applicationPath = Path.GetFullPath(Environment.CurrentDirectory);
			rootResourcePath = Path.Combine(applicationPath, ResourceConstants.RESOURCE_ROOT_DIR_REL_PATH);
			coreResourcePath = Path.Combine(rootResourcePath, ResourceConstants.RESOURCE_CORE_DIR_REL_PATH);
			modsResourcePath = Path.Combine(rootResourcePath, ResourceConstants.RESOURCE_MODS_DIR_REL_PATH);
		}

		#endregion
		#region Fields

		private readonly ResourceManager resourceManager;

		public readonly string applicationPath = string.Empty;
		public readonly string rootResourcePath = string.Empty;
		private readonly string coreResourcePath = string.Empty;
		private readonly string modsResourcePath = string.Empty;

		private readonly List<LibraryDir> resourceLibraries = new(32);

		#endregion
		#region Properties

		/// <summary>
		/// Gets the total number of library directories that resources may be loaded from.
		/// </summary>
		public int LibraryCount => resourceLibraries.Count;

		#endregion
		#region Methods

		/// <summary>
		/// Retrieve the path and type of a specific resource library.
		/// </summary>
		/// <param name="_index">The index of the library you want. Must be non-negative and less than '<see cref="LibraryCount"/>'.</param>
		/// <param name="_outPath">Outputs an absolute path of the directory the library is located in.</param>
		/// <param name="_outSource">Outputs the source or type of library this is. Whether its a core library, application resources, or modded content.</param>
		/// <returns>True if the index was valid, false otherwise.</returns>
		public bool GetLibrary(int _index, out string _outPath, out ResourceSource _outSource)
		{
			if (_index >= 0 && _index < resourceLibraries.Count)
			{
				LibraryDir libDir = resourceLibraries[_index];
				_outPath = libDir.path;
				_outSource = libDir.source;
				return true;
			}

			_outPath = string.Empty;
			_outSource = ResourceSource.Runtime;
			return false;
		}

		/// <summary>
		/// Find and register all resource files within the application's data directories.
		/// </summary>
		/// <param name="_updateExistingResources">Whether to update (i.e. replace) any of the existing files and resources on reload.
		/// If true, resources that have already been registered will be unloaded first and then re-registered from the latest file contents.
		/// If false, any files and resources that have already been registered will be skipped during the gathering process. The latter is
		/// useful when checking for new data at run-time, whilst the former should be used sparingly and at launch time.</param>
		/// <returns>True if all libraries were detected, and files and resources therein were registered successfully. False otherwise.</returns>
		public bool GatherAllResourceFiles(out Progress _outProgress, bool _updateExistingResources = false)
		{
			Console.WriteLine("\n# Gathering all resource files...");

			_outProgress = new Progress("Gathering all resource files", 0);

			Console.WriteLine($"- Verifying resource paths...");
			if (!VerifyResourcePaths(_outProgress))
			{
				_outProgress.Finish();
				return false;
			}

			Console.WriteLine($"+ Gathering all libraries...");
			if (!GatherAllLibraries(_outProgress))
			{
				_outProgress.Finish();
				return false;
			}

			Console.WriteLine($"- Updating mod load order...");
			if (!UpdateModLoadOrder(_outProgress))
			{
				_outProgress.Finish();
				return false;
			}

			Console.WriteLine("\n# Loading resource libraries...");

			int taskCount = 3 + resourceLibraries.Count;
			_outProgress.Update("Loading resource libraries", 3, taskCount);

			List<string> metadataFiles = new(32);
			List<string> unassignedDataFilesAbs = new(32);
			List<string> pairedDataFiles = new(32);
			List<string> looseDataFiles = new(32);

			List<ResourceHandle> resourceHandleBuffer = new(32);

			foreach (LibraryDir libDir in resourceLibraries)
			{
				Console.Write($"- Importing resource library '{libDir.name}'...");
				try
				{
					string[] files = Directory.GetFiles(libDir.path);

					// Differentiate between metadata and data files:
					foreach (string file in files)
					{
						string ext = Path.GetExtension(file).ToLowerInvariant();
						if (ext == ResourceConstants.FILE_EXT_METADATA)
						{
							metadataFiles.Add(Path.GetRelativePath(applicationPath, file));
						}
						else if (ext == ResourceConstants.FILE_EXT_BATCH_BLOCK_COMPRESSED)
						{
							// Block-compresssed batch files *will* (rather, must) have a metadata file attached.
							pairedDataFiles.Add(Path.GetRelativePath(applicationPath, file));
						}
						else
						{
							unassignedDataFilesAbs.Add(file);
						}
					}

					// Differentiate between loose data files and data-metadata file pairs:
					while (unassignedDataFilesAbs.Count > 0)
					{
						string dataFile = Path.GetRelativePath(applicationPath, unassignedDataFilesAbs.Last());
						string metadataFile = Path.ChangeExtension(dataFile, ResourceConstants.FILE_EXT_METADATA);
						if (metadataFiles.Contains(metadataFile))
						{
							pairedDataFiles.Add(dataFile);
						}
						else
						{
							looseDataFiles.Add(dataFile);
						}
						unassignedDataFilesAbs.RemoveAt(unassignedDataFilesAbs.Count - 1);
					}

					// Process and create handles for all files and the contained resources:
					foreach (string metadataFilePath in metadataFiles)
					{
						resourceHandleBuffer.Clear();
						if (ResourceFileHandle.CreateFileHandle(resourceManager, metadataFilePath, libDir.source, out ResourceFileHandle fileHandle, ref resourceHandleBuffer))
						{
							TryRegisterFileHandle(fileHandle, resourceHandleBuffer, _updateExistingResources);
						}
					}
					foreach (string looseDataFile in looseDataFiles)
					{
						resourceHandleBuffer.Clear();
						if (ResourceFileHandle.CreateFileHandle(resourceManager, looseDataFile, libDir.source, out ResourceFileHandle fileHandle, ref resourceHandleBuffer))
						{
							TryRegisterFileHandle(fileHandle, resourceHandleBuffer, _updateExistingResources);
						}
					}
					Console.WriteLine(" done.");
					_outProgress.Increment();
				}
				catch (Exception ex)
				{
					_outProgress.errorCount++;
					Console.WriteLine(" FAIL.");
					Console.WriteLine($"Error! Failed to gather resource files for resource library '{libDir.name}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
					if (libDir.source == ResourceSource.Core)
					{
						Console.WriteLine($"Error! Core resource library '{libDir.name}' could not be processed! Aborting resource gathering!");
						return false;
					}
				}

				// Clear all buffer lists for the next library:
				metadataFiles.Clear();
				unassignedDataFilesAbs.Clear();
				pairedDataFiles.Clear();
				looseDataFiles.Clear();
			}

			Console.WriteLine("\n# Finished loading resource libraries.");

			_outProgress.Finish();
			return true;
		}

		private bool TryRegisterFileHandle(ResourceFileHandle _fileHandle, List<ResourceHandle> _resourceHandles, bool _updateExistingResources)
		{
			if (_fileHandle == null) return false;

			bool success = true;

			// Register or replace file handle:
			if (resourceManager.HasFile(_fileHandle.Key))
			{
				if (!_updateExistingResources) return false;
				success &= resourceManager.RemoveFile(_fileHandle.Key);
			}
			success &= resourceManager.AddFile(_fileHandle);

			// Register or replace resource handles:
			foreach (ResourceHandle resHandle in _resourceHandles)
			{
				if (resourceManager.HasResource(resHandle.Key))
				{
					if (_updateExistingResources) continue;
					success &= resourceManager.RemoveResource(resHandle.Key);
				}
				success &= resourceManager.AddResource(resHandle);
			}

			return success;
		}

		/// <summary>
		/// Check if all root and core library directories exist where they are supposed to, or create any parts of the folder hierarchy that is missing.
		/// </summary>
		/// <returns>True if all directories are accounted for, or if they could at least be re-created, false otherwise.</returns>
		public bool VerifyResourcePaths(Progress? _progress)
		{
			if (string.IsNullOrEmpty(rootResourcePath))
			{
				Console.WriteLine("Error! Resource root path cannot not be null or blank!");
				if (_progress != null) _progress.errorCount++;
				return false;
			}

			int taskCount = 3 + ResourceConstants.coreResourceLibraries.Length;
			_progress?.Update("Verifying resource paths", 0, taskCount);

			// Recreate root resource directory if it wasn't found:
			if (!Directory.Exists(rootResourcePath))
			{
				try
				{
					Directory.CreateDirectory(rootResourcePath);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error! Failed to create resource root directory at path '{rootResourcePath}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
					if (_progress != null) _progress.errorCount++;
					return false;
				}
				_progress?.Increment();
			}

			// Ensure core libraries exist:
			try
			{
				if (!Directory.Exists(coreResourcePath))
				{
					Directory.CreateDirectory(coreResourcePath);
				}
				_progress?.Increment();

				// Do the same for all sub-directories of the core library folder:
				foreach (string subDir in ResourceConstants.coreResourceLibraries)
				{
					string subDirPath = Path.Combine(coreResourcePath, subDir);
					if (!Directory.Exists(subDirPath))
					{
						Directory.CreateDirectory(subDirPath);
					}
					_progress?.Increment();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to create core resource library directories at path '{coreResourcePath}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				if (_progress != null) _progress.errorCount++;
				return false;
			}
			if (_progress != null) _progress.tasksDone = 2 + ResourceConstants.coreResourceLibraries.Length;

			// Ensure parent directory for modded content exists:
			if (!Directory.Exists(modsResourcePath))
			{
				try
				{
					Directory.CreateDirectory(modsResourcePath);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error! Failed to create modded content directory at path '{modsResourcePath}'!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
					if (_progress != null) _progress.errorCount++;
					return false;
				}
			}
			_progress?.Increment();

			_progress?.CompleteAllTasks();
			return true;
		}

		/// <summary>
		/// Update list of registered libraries by scanning for library folders within the root resource directory (i.e. '<see cref="rootResourcePath"/>').
		/// </summary>
		/// <returns>True if all directories could be found and registered, false if an IO exception was thrown.</returns>
		private bool GatherAllLibraries(Progress? _progress)
		{
			resourceLibraries.Clear();

			_progress?.Update("Gathering resource libraries", 0, 1);

			try
			{
				// Find all applicable directories:
				string[] coreSubDirs = Directory.GetDirectories(coreResourcePath);
				string[] appDirs = Directory.GetDirectories(rootResourcePath);
				string[] modDirs = Directory.GetDirectories(modsResourcePath);

				int taskCount = coreSubDirs.Length + Math.Max(appDirs.Length - 2, 0) + modDirs.Length + 1;
				_progress?.Update(null, 1, taskCount);

				// 1. Add all core libraries:
				int coreSubDirsLoaded = 0;
				foreach (string coreSubDir in coreSubDirs)
				{
					string absPath = Path.GetFullPath(coreSubDir);
					string dirName = $"{ResourceConstants.RESOURCE_CORE_DIR_REL_PATH}/{PathTools.GetLastPartName(coreSubDir)}";
					resourceLibraries.Add(new(absPath, dirName, ResourceSource.Core));

					_progress?.Increment();
					coreSubDirsLoaded++;
				}
				Console.WriteLine($"  - Core libraries: {coreSubDirsLoaded}");

				// 2. Find all custom libraries:
				int appDirsLoaded = 0;
				foreach (string appDir in appDirs)
				{
					string dirName = PathTools.GetLastPartName(appDir);
					if (dirName != ResourceConstants.RESOURCE_CORE_DIR_REL_PATH &&
						dirName != ResourceConstants.RESOURCE_MODS_DIR_REL_PATH)
					{
						string absPath = Path.GetFullPath(appDir);
						resourceLibraries.Add(new(absPath, dirName, ResourceSource.Application));

						_progress?.Increment();
						appDirsLoaded++;
					}
				}
				Console.WriteLine($"  - Application libraries: {appDirsLoaded}");

				// 3. Add all modded contents:
				int modDirsLoaded = 0;
				foreach (string modDir in modDirs)
				{
					string absPath = Path.GetFullPath(modDir);
					string dirName = $"{ResourceConstants.RESOURCE_MODS_DIR_REL_PATH}/{PathTools.GetLastPartName(modDir)}";
					resourceLibraries.Add(new(absPath, dirName, ResourceSource.Mod));

					_progress?.Increment();
					modDirsLoaded++;
				}
				Console.WriteLine($"  - Mod libraries: {modDirsLoaded}");
			}
			catch (Exception ex)
			{
				if (_progress != null) _progress.errorCount++;
				Console.WriteLine($"Error! Failed to gather resource library directories!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				return false;
			}

			// Note: Most resource libraries are already sorted in their correct load order at this stage. Mods are an exception; their load order is adjusted later.
			_progress?.CompleteAllTasks();
			return true;
		}

		/// <summary>
		/// Ensure that mod libraries are registered and listed in the order that matches their load order, with later entries overwriting changes done by earlier entries.
		/// </summary>
		/// <returns></returns>
		private bool UpdateModLoadOrder(Progress? _progress)
		{
			// Find all mod folders:
			List<LibraryDir> modLibPaths = resourceLibraries.Where(o => o.source == ResourceSource.Mod).ToList();
			if (modLibPaths == null || modLibPaths.Count == 0)
			{
				return true;
			}

			_progress?.Update("Updating mod load order", 0, modLibPaths.Count);

			//TODO: Define some mod configuration file/structure and read mod load order from there, then sort mod libraries according to it.


			_progress?.CompleteAllTasks();
			return true;
		}

		#endregion
	}
}