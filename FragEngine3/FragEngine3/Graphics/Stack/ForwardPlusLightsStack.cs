using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Stack
{
	public sealed class ForwardPlusLightsStack : IGraphicsStack
	{
		#region Types

		private sealed class RendererList
		{
			public RendererList(RenderMode _mode, int _initialCapacity)
			{
				mode = _mode;
				renderers = new(_initialCapacity);
			}

			public readonly RenderMode mode;
			public readonly List<IRenderer> renderers;
			public CommandList? cmdList = null;

			public void Clear() => renderers.Clear();
		}

		#endregion
		#region Constructors

		public ForwardPlusLightsStack(GraphicsCore _core)
		{
			core = _core ?? throw new ArgumentNullException(nameof(core), "Graphics core may not be null!");
		}
		~ForwardPlusLightsStack()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly GraphicsCore core;

		private bool isInitialized = false;
		private bool isDrawing = false;

		private static readonly int[] rendererModeIndices = new int[]
		{
			0,		// RenderMode.Compute
			1,		// RenderMode.Opaque
			2,		// RenderMode.Transparent
			-1,		// RenderMode.Volumetric
			-1,		// RenderMode.PostProcessing
			3,		// RenderMode.UI
			-1,		// RenderMode.Custom
		};
		private readonly RendererList[] rendererLists = new RendererList[]
		{
			new RendererList(RenderMode.Compute, 4),
			new RendererList(RenderMode.Opaque, 128),
			new RendererList(RenderMode.Transparent, 32),
			new RendererList(RenderMode.UI, 32),
		};

		private readonly object lockObj = new();

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public bool IsValid => !IsDisposed && core.IsInitialized && Scene != null && !Scene.IsDisposed;	//TODO
		public bool IsInitialized => !IsDisposed && isInitialized;
		public bool IsDrawing => IsInitialized && isDrawing;

		public Scene? Scene { get; private set; } = null;

		public int VisibleRendererCount { get; private set; } = 0;
		public int SkippedRendererCount { get; private set; } = 0;

		private Logger Logger => core.graphicsSystem.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _disposing)
		{
			if (isInitialized)
			{
				Shutdown();
			}

			IsDisposed = true;

			//...
		}

		public bool Initialize(Scene _scene)
		{
			lock(lockObj)
			{
				if (IsDisposed)
				{
					Logger.LogError("Cannot initialize disposed Forward+Lights graphics stack!");
					return false;
				}
				if (_scene == null || _scene.IsDisposed)
				{
					Logger.LogError("Cannot initialize graphics stack for null or disposed scene!");
					return false;
				}

				Scene = _scene;

				//...

				Logger.LogMessage($"Initialized graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene.Name}'.");

				isInitialized = true;
			}
			return true;
		}

		public void Shutdown()
		{
			if (!isInitialized) return;

			lock(lockObj)
			{
				foreach (RendererList rendererList in rendererLists)
				{
					rendererList.Clear();
				}

				VisibleRendererCount = 0;
				SkippedRendererCount = 0;

				Scene = null;

				isDrawing = false;
				isInitialized = false;
			}

			Logger.LogMessage($"Shut down graphics stack of type '{nameof(ForwardPlusLightsStack)}'.");
		}

		public bool Reset()
		{
			Logger.LogMessage($"Resetting graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene?.Name ?? "NULL"}'.");

			if (IsDisposed)
			{
				Logger.LogError("Cannot reset disposed Forward+Lights graphics stack!");
				return false;
			}

			// Simply shut down the whole stack:
			Shutdown();

			if (!IsValid)
			{
				Logger.LogError("Cannot reinitialize invalid Forward+Lights graphics stack!");
				return false;
			}

			// Then reinitialize it back to its starting state:
			return Initialize(Scene!);
		}

		public bool GetRenderTargets(out Framebuffer? _outRenderTargets)
		{
			throw new NotImplementedException();		//TODO
		}

		public bool DrawStack(Scene _scene, List<SceneNodeRendererPair> _nodeRendererPairs)
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot draw uninitialized Forward+Lights graphics stack!");
				return false;
			}
			if (_scene != Scene || Scene.IsDisposed)
			{
				Logger.LogError("Cannot draw graphics stack for null or mismatched scene!");
				return false;
			}
			if (_nodeRendererPairs == null)
			{
				Logger.LogError("Cannot draw graphics stack for null list of node-renderers pairs!");
				return false;
			}

			bool success = true;
			isDrawing = true;
			
			try
			{
				lock (lockObj)
				{
					//TODO: Get camera from scene!
					//TODO: Repeat this process for each active camera!
					uint cameraRenderFlags = 0xFFFFFFFFu;

					// Clear out all renderer lists for the upcoming frame:
					foreach (RendererList rendererList in rendererLists)
					{
						rendererList.Clear();
					}
					VisibleRendererCount = 0;
					SkippedRendererCount = 0;

					// No nodes and no scene behaviours? Skip drawing altogether:
					if (_nodeRendererPairs.Count == 0 && _scene.SceneBehaviourCount == 0)
					{
						return true;
					}

					// Assign each renderer to the most appropriate rendering list:
					foreach (SceneNodeRendererPair pair in _nodeRendererPairs)
					{
						if (pair.renderer.IsVisible && (pair.renderer.LayerFlags & cameraRenderFlags) != 0)
						{
							// Skip any renderers that cannot be mapped to any of the supported modes:
							if (GetRendererListForMode(pair.renderer.RenderMode, out RendererList? rendererList))
							{
								SkippedRendererCount++;
								continue;
							}

							// Add the renderer to its mode's corresponding list:
							rendererList!.renderers.Add(pair.renderer);

							VisibleRendererCount++;
						}
					}

					if (VisibleRendererCount != 0)
					{
						// Issue draw calls for each renderer list:
						success &= DrawOpaqueRendererList();
						success &= DrawZSortedRendererList();
						success &= DrawUiRendererList();


						//TODO: Composite end results?
					
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException($"An exception was caught while trying to draw scene using graphics stack of type '{nameof(ForwardPlusLightsStack)}'!", ex);
				return false;
			}
			
			isDrawing = false;
			return success;
		}

		private bool GetRendererListForMode(RenderMode _mode, out RendererList? _outRendererList)
		{
			int modeIdx = (int)_mode;
			int rendererListIdx = rendererModeIndices[modeIdx];

			if (rendererListIdx >= 0)
			{
				_outRendererList = rendererLists[rendererListIdx];
				return true;
			}
			_outRendererList = null;
			return false;
		}

		private bool DrawOpaqueRendererList()
		{
			if (!GetRendererListForMode(RenderMode.Opaque, out RendererList? opaqueList) || opaqueList == null)
			{
				return false;
			}

			if (opaqueList.renderers.Count == 0) return true;

			// Ensure the command list is set:
			if (opaqueList.cmdList == null || opaqueList.cmdList.IsDisposed)
			{
				if (!core.CreateCommandList(out opaqueList.cmdList) || opaqueList.cmdList == null)
				{
					Logger.LogError("Failed to create command list for opaque renderer list!");
					opaqueList.cmdList?.Dispose();
					opaqueList.cmdList = null;
					return false;
				}
			}

			bool success = true;

			// Draw list of renderers as-is:
			foreach (IRenderer renderer in opaqueList.renderers)
			{
				success &= renderer.Draw(opaqueList.cmdList);
			}

			return success;
		}

		private bool DrawZSortedRendererList()
		{
			if (!GetRendererListForMode(RenderMode.Transparent, out RendererList? zSortedList) || zSortedList == null)
			{
				return false;
			}

			if (zSortedList.renderers.Count == 0) return true;

			// Ensure the command list is set:
			if (zSortedList.cmdList == null || zSortedList.cmdList.IsDisposed)
			{
				if (!core.CreateCommandList(out zSortedList.cmdList) || zSortedList.cmdList == null)
				{
					Logger.LogError("Failed to create command list for Z-sorted renderer list!");
					zSortedList.cmdList?.Dispose();
					zSortedList.cmdList = null;
					return false;
				}
			}

			bool success = true;

			// Sort all transparent renderers by their Z-depth: (aka distance to camera)
			Vector3 viewportPosition = Vector3.Zero;        //TODO: Implement camera type, then use currently rendering camera's position for this!
			Vector3 cameraDirection = Vector3.UnitZ;		//TODO: Consider pre-calculating this for all renderers in list, to avoid recalculating for each comparison!

			zSortedList.renderers.Sort((a, b) => a.GetZSortingDepth(viewportPosition, cameraDirection).CompareTo(b.GetZSortingDepth(viewportPosition, cameraDirection)));

			// Draw Z-sorted list of renderers:
			foreach (IRenderer renderer in zSortedList.renderers)
			{
				success &= renderer.Draw(zSortedList.cmdList);
			}

			return success;
		}

		private bool DrawUiRendererList()
		{
			if (!GetRendererListForMode(RenderMode.Opaque, out RendererList? uiList) || uiList == null)
			{
				return false;
			}

			if (uiList.renderers.Count == 0) return true;

			// Ensure the command list is set:
			if (uiList.cmdList == null || uiList.cmdList.IsDisposed)
			{
				if (!core.CreateCommandList(out uiList.cmdList) || uiList.cmdList == null)
				{
					Logger.LogError("Failed to create command list for UI renderer list!");
					uiList.cmdList?.Dispose();
					uiList.cmdList = null;
					return false;
				}
			}

			bool success = true;

			// Draw list of renderers in strictly hierarchical order:
			foreach (IRenderer renderer in uiList.renderers)
			{
				success &= renderer.Draw(uiList.cmdList);
			}

			return success;
		}

		#endregion
	}
}
