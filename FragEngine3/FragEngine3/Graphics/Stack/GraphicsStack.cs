using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Stack
{
	public sealed class GraphicsStack : IDisposable
	{
		#region Types

		private sealed class ParallelLayerGroup
		{
			public ParallelLayerGroup(int _startIdx)
			{
				startIndex = _startIdx;
				endIndex = _startIdx;
			}

			public readonly List<GraphicsStackLayer> layers = new(4);
			public readonly List<CommandList> cmdLists = new(4);

			public readonly int startIndex = 0;
			public int endIndex = 0;
			
			public int LayerCount => layers.Count;
		}

		#endregion
		#region Constructors

		public GraphicsStack(GraphicsCore _core)
		{
			core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");
		}
		~GraphicsStack()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly GraphicsCore core;

		public readonly List<GraphicsStackLayer> layers = new();
		private readonly List<ParallelLayerGroup> parallelizedGroups = new();
		private readonly Stack<CommandList> commandListPool = new();

		private Scene? lastDrawnScene = null;
		private int lastDrawnSceneState = -1;

		private readonly List<Component> allRenderers = new(64);

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		/// <summary>
		/// Gets or sets whether to draw the final layers' output directly to main swapchain's framebuffer.
		/// </summary>
		public bool OutputToMainSwapchain { get; set; } = true;

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

			// Get rid of all command lists we're hoarding:
			ReturnCommandListsToPool();
			while (commandListPool.TryPop(out CommandList? cmdList))
			{
				if (cmdList != null && !cmdList.IsDisposed) cmdList.Dispose();
			}

			// Purge all layers:
			foreach (GraphicsStackLayer layer in layers)
			{
				if (layer != null && !layer.IsDisposed) layer.Dispose();
			}

			// Drop all references to disposed objects:
			if (_disposing)
			{
				commandListPool.Clear();
				allRenderers.Clear();
				layers.Clear();
			}
		}

		public bool IsValid()
		{
			if (IsDisposed) return false;
			if (!core.IsInitialized) return false;

			bool result = true;

			foreach (GraphicsStackLayer layer in layers)
			{
				if (layer != null && !layer.IsDisposed)
				{
					result &= layer.IsValid();
				}
			}

			return result;
		}

		private void ReturnCommandListsToPool()
		{
			foreach (ParallelLayerGroup prevGroup in parallelizedGroups)
			{
				foreach (CommandList cmdList in prevGroup.cmdLists)
				{
					if (!cmdList.IsDisposed && cmdList != core.MainCommandList)
					{
						commandListPool.Push(cmdList);
					}
				}
			}
			parallelizedGroups.Clear();
		}

		public bool RebuildStack(Scene _scene)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot rebuild disposed graphics stack!");
				return false;
			}
			if (_scene == null || _scene.IsDisposed)
			{
				Console.WriteLine("Error! Cannot rebuild graphics stack for null or disposed scene!");
				return false;
			}

			bool success = true;

			Console.WriteLine("+ Rebuilding graphics stack...");

			// Clean up expired layers and groups:
			layers.RemoveAll(o => o == null || o.IsDisposed);
			ReturnCommandListsToPool();

			// Gather a list of all renderers within the scene:
			allRenderers.Clear();

			//TODO: Gather all renderers in the scene.

			// Rebuild each stack layer in order:
			int rebuildCount = 0;
			int parallelCount = 0;
			int[] parallelizationStartIndices = new int[layers.Count];
			for (int i = 0; i < layers.Count; ++i)
			{
				GraphicsStackLayer layer = layers[i];

				success &= layer.RebuildLayer(_scene, allRenderers);
				rebuildCount++;

				// Determine if and how far this layer can run in parallel to other layers: (last layer is non-parallelized for final composition)
				int parallelizationStartIdx = i;
				if (i < layers.Count - 1 && layer.CanLayerBeParallelized(i, out int firstCompatibleIdx) && firstCompatibleIdx >= 0)
				{
					parallelizationStartIdx = firstCompatibleIdx;
					parallelCount++;
				}
				parallelizationStartIndices[i] = parallelizationStartIdx;
			}
			if (!success)
			{
				Console.WriteLine("Error! Failed to rebuild all layers in graphics stack!");
				return false;
			}
			Console.WriteLine($"  - Rebuilt layers: {rebuildCount}");
			Console.WriteLine($"  - Parallelizable: {parallelCount}");
			Console.WriteLine($"  - Unassigned renderers: {allRenderers.Count}");

			// Go through layers and group those that can be parallelized:
			ParallelLayerGroup? curLayerGroup = null;
			ParallelLayerGroup? prevLayerGroup = null;
			for (int i = 0; i < layers.Count; ++i)
			{
				// Check if this and all following layers are single-threaded only and non-parallelizable:
				bool parallelizeWithNext = i < layers.Count - 1 && parallelizationStartIndices[i + 1] > i;
				bool isFinalSingleThreadStretch = OutputToMainSwapchain && !parallelizeWithNext;
				if (isFinalSingleThreadStretch)
				{
					for (int j = i; j < layers.Count; ++j)
					{
						if (parallelizationStartIndices[j] < j)
						{
							isFinalSingleThreadStretch = false;
							break;
						}
					}
				}

				CommandList cmdList = null!;

				// First group, acquire new command list:
				if (curLayerGroup == null)
				{
					curLayerGroup = new(i);
					parallelizedGroups.Add(curLayerGroup);

					success &= GetCommandList(out cmdList!, isFinalSingleThreadStretch);
				}
				// Current layer can be parallelized with ongoing group's layers, create command list for each layer:
				else if (parallelizeWithNext || curLayerGroup.startIndex >= parallelizationStartIndices[i])
				{
					success &= GetCommandList(out cmdList!, isFinalSingleThreadStretch);
				}
				// Layer cannot be parallelized with previous, start new group:
				else
				{
					prevLayerGroup = curLayerGroup;
					curLayerGroup = new(i);
					parallelizedGroups.Add(curLayerGroup);

					// If previous group was single-layer, continue its command list:
					if (prevLayerGroup != null && prevLayerGroup.LayerCount == 1)
					{
						cmdList = prevLayerGroup.cmdLists[0];
					}
					// If previous group was parallelized, start new command list:
					else
					{
						success &= GetCommandList(out cmdList!, isFinalSingleThreadStretch);
					}
				}

				curLayerGroup.layers.Add(layers[i]);
				curLayerGroup.cmdLists.Add(cmdList);
			}
			if (!success)
			{
				Console.WriteLine("Error! Failed to group graphics stack layers for parallelization!");
				return false;
			}
			Console.WriteLine($"  - Layer groups: {parallelizedGroups.Count}");
			Console.WriteLine($"  - Max. parallelized: {parallelizedGroups.Max(o => o.LayerCount)}");

			return true;
		}

		public bool DrawStack(Scene _scene)
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot draw disposed graphics stack!");
				return false;
			}

			// Determine whether the stack is up-to-date or needs rebuilding:
			bool rebuildStack = false;

			if (lastDrawnScene != _scene || lastDrawnSceneState != _scene.DrawStackState)
			{
				lastDrawnScene = _scene;
				lastDrawnSceneState = _scene.DrawStackState;
				rebuildStack = true;
			}

			// Rebuild stack just-in-time and on demand:
			if (rebuildStack && !RebuildStack(_scene))
			{
				Console.WriteLine($"Error! Failed to rebuild graphics stack for scene '{_scene}'!");
				return false;
			}

			int errorCounter = 0;
			CommandList? prevCmdList = null;
			for (int i = 0; i < parallelizedGroups.Count; ++i)
			{
				ParallelLayerGroup group = parallelizedGroups[i];

				// For multiple layers in a group, parallelize draw call generation:
				if (group.LayerCount > 1)
				{
					Parallel.For(0, group.LayerCount, (layerIdx) =>
					{
						GraphicsStackLayer layer = group.layers[layerIdx];
						CommandList cmdList = group.cmdLists[layerIdx];
						if (!layer.IsEmpty)
						{
							bool result = layer.GetRenderTarget(out Framebuffer framebuffer);
							if (result)
							{
								cmdList.Begin();
								cmdList.SetFramebuffer(framebuffer);

								result &= layer.DrawLayer(_scene, cmdList);

								cmdList.End();
								result &= core.CommitCommandList(cmdList);
							}
							if (!result) Interlocked.Increment(ref errorCounter);
						}
					});
					prevCmdList = null;
				}
				// For single-layer groups, generate draw calls on main thread only:
				else
				{
					GraphicsStackLayer layer = group.layers[0];
					if (layer.IsEmpty) continue;

					CommandList cmdList = group.cmdLists[0];

					bool cmdListHasChanged = cmdList != prevCmdList;
					bool cmdListWillChange =
						i == parallelizedGroups.Count - 1 ||				// if last group.
						parallelizedGroups[i + 1].LayerCount != 1 ||		// if next is parallelized
						parallelizedGroups[i + 1].cmdLists[0] != cmdList;	// if next uses other list

					bool result = true;
					if (result)
					{
						// Initialize command list if switching to a new one:
						if (cmdListHasChanged)
						{
							result &= layer.GetRenderTarget(out Framebuffer framebuffer);

							cmdList.Begin();
							cmdList.SetFramebuffer(framebuffer);
						}

						if (result)
						{
							// Draw layer renderers:
							result &= layer.DrawLayer(_scene, cmdList);

							// Finalize and submit command list if last or next group uses different ones:
							if (cmdListWillChange && cmdList != core.MainCommandList)
							{
								cmdList.End();
								result &= core.CommitCommandList(cmdList);
							}
						}
					}
					if (!result) errorCounter++;
					prevCmdList = cmdList;
				}

				// Abort the rendering process if any of the layers encountered a major error:
				if (errorCounter != 0)
				{
					Console.WriteLine($"Error! Graphics stack layer group {i}/{parallelizedGroups.Count} failed to complete! Aborting...");
					break;
				}
			}

			return errorCounter != 0;
		}

		/// <summary>
		/// Reuse a command list from the pool, or create a new one.
		/// </summary>
		/// <param name="_outCmdList">Ouputs a command list that may be used to record draw calls by a layer.
		/// If '<see cref="_finalSingleThreadedLayers"/>' is true, the graphics core's main command list will
		/// be returned. In all other cases, a different list will either be drawn from the pool of previously
		/// create command lists, or a new one will be created. Null if no command list could be acquired.</param>
		/// <returns>true if a command list could be retrieved or created, false otherwise.</returns>
		private bool GetCommandList(out CommandList? _outCmdList, bool _finalSingleThreadedLayers)
		{
			// On the final single-threaded-only set of layers at the end of the stack, use the core's main command list:
			if (_finalSingleThreadedLayers)
			{
				_outCmdList = core.MainCommandList;
				return true;
			}

			// Try recycling a previously allocated command list from our pool:
			if (commandListPool.TryPop(out _outCmdList) && _outCmdList != null)
			{
				return true;
			}

			// Try creating a new list if none were ready to use:
			if (core.CreateCommandList(out _outCmdList) && _outCmdList != null)
			{
				return true;
			}

			// This code should never be reached, as it means the GPU and graphics core are no longer operational. Good luck:
			Console.WriteLine("Error! Failed to get or create command list; fatal exceptions may be imminent!");
			_outCmdList = null;
			return false;
		}

		#endregion
	}
}

