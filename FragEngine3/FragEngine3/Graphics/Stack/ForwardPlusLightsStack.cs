using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Stack
{
	public sealed class ForwardPlusLightsStack(GraphicsCore _core) : IGraphicsStack
	{
		#region Types

		private sealed class RendererList(RenderMode _mode, int _initialCapacity)
		{
			public readonly RenderMode mode = _mode;
			public readonly List<IRenderer> renderers = new(_initialCapacity);
			public CommandList? cmdList = null;

			public void Clear() => renderers.Clear();
		}

		#endregion
		#region Constructors
		~ForwardPlusLightsStack()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly GraphicsCore core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

		private bool isInitialized = false;
		private bool isDrawing = false;

		private static readonly int[] rendererModeIndices =
		[
			0,		// RenderMode.Compute
			1,		// RenderMode.Opaque
			2,		// RenderMode.Transparent
			-1,		// RenderMode.Volumetric
			-1,		// RenderMode.PostProcessing
			3,		// RenderMode.UI
			-1,		// RenderMode.Custom
		];
		private readonly RendererList[] rendererLists =
		[
			new(RenderMode.Compute, 4),
			new(RenderMode.Opaque, 128),
			new(RenderMode.Transparent, 32),
			new(RenderMode.UI, 32),
		];

		private readonly List<Light> cameraLightBuffer = new(64);
		private Light.LightSourceData[] lightSourceDataBuffer = new Light.LightSourceData[32];

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
		public int FailedRendererCount { get; private set; } = 0;

		private Logger Logger => core.graphicsSystem.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _)
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

				VisibleRendererCount = 0;
				SkippedRendererCount = 0;
				FailedRendererCount = 0;

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
				cameraLightBuffer.Clear();

				foreach (RendererList rendererList in rendererLists)
				{
					rendererList.Clear();
				}

				VisibleRendererCount = 0;
				SkippedRendererCount = 0;
				FailedRendererCount = 0;

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

		public bool DrawStack(Scene _scene, List<IRenderer> _renderers, in IList<Camera> _cameras, in IList<Light> _lights)
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
			if (_renderers == null)
			{
				Logger.LogError("Cannot draw graphics stack for null list of node-renderers pairs!");
				return false;
			}
			if (_cameras == null)
			{
				Logger.LogError("Cannot draw graphics stack using null camera list!");
				return false;
			}

			cameraLightBuffer.Clear();

			// Skip rendering if there are no cameras in the scene:
			if (_cameras.Count == 0)
			{
				VisibleRendererCount = 0;
				SkippedRendererCount = 0;
				isDrawing = false;
				return true;
			}

			bool success = true;
			isDrawing = true;

			uint maxActiveLightCount = Math.Max(core.graphicsSystem.Settings.MaxActiveLightCount, 1);

			// Draw scene for each of the scene's active cameras:
			bool[] camerasAreLive = new bool[_cameras.Count];
			for (int i = 0; i < _cameras.Count; ++i)
			{
				// Skip any cameras that are expired or disabled:
				Camera camera = _cameras[i];
				if (camera == null || camera.IsDisposed || !camera.node.IsEnabledInHierarchy() || camera.layerMask == 0u)
				{
					camerasAreLive[i] = false;
					continue;
				}
				camerasAreLive[i] = true;

				//TEST TEST TEST TEST
				camera.SetOverrideRenderTargets(core.Device.SwapchainFramebuffer, false);
				//TEST TEST TEST TEST

				GatherLightsForCamera(in _lights, camera);

				success &= TryRenderCamera(_scene, _renderers, camera, maxActiveLightCount);
			}

			/*
			// Composite results to backbuffer:
			{
				CommandList mainCmdList = core.MainCommandList;
				Framebuffer backBuffer = core.Device.SwapchainFramebuffer;
				Texture backBufferColorTarget = backBuffer.ColorTargets[0].Target;
				Texture backBufferDepthTarget = backBuffer.DepthTarget!.Value.Target;	//TODO: This is a temporary and overly pathetic way of throwing content at the backbuffer. Change this to use an actual composition shader!

				for (int i = 0; i < _cameras.Count; ++i)
				{
					if (!camerasAreLive[i]) continue;

					Camera camera = _cameras[i];
					if (camera.GetActiveRenderTargets(out Framebuffer? framebuffer) && framebuffer != null)
					{
						Texture colorTarget = framebuffer.ColorTargets[0].Target;
						mainCmdList.CopyTexture(colorTarget, backBufferColorTarget);

						Texture? depthTarget = framebuffer?.DepthTarget?.Target;
						if (depthTarget != null)
						{
							mainCmdList.CopyTexture(depthTarget, backBufferDepthTarget);
						}
					}
				}
			}
			*/

			isDrawing = false;
			return success;
		}

		private void GatherLightsForCamera(in IList<Light> _lights, Camera _camera)
		{
			// Identify all relevant lights for this camera's viewport:
			foreach (Light light in _lights)
			{
				if (!light.IsDisposed &&
					light.node.IsEnabledInHierarchy() &&
					(light.layerMask & _camera.layerMask) != 0)
				{
					if (light.Type != Light.LightType.Directional)
					{
						// TODO: Determine if light's maximum range overlaps viewport frustum!
						cameraLightBuffer.Add(light);
					}
					else
					{
						cameraLightBuffer.Add(light);
					}
				}
			}
		}

		private bool TryRenderCamera(Scene _scene, List<IRenderer> _renderers, Camera _camera, uint _maxActiveLightCount)
		{
			try
			{
				lock (lockObj)
				{
					// Get or create CPU and CPU-side buffers for assembling and binding light data:
					uint activeLightCount = Math.Clamp((uint)cameraLightBuffer.Count, 0, _maxActiveLightCount);
					if (!_camera.GetLightDataBuffer(activeLightCount, out DeviceBuffer? lightDataBuffer) || lightDataBuffer == null)
					{
						Logger.LogError("Failed to get or create camera's light source data buffer!");
						return false;
					}
					if (lightSourceDataBuffer.Length < activeLightCount)
					{
						lightSourceDataBuffer = new Light.LightSourceData[activeLightCount];
					}
					// Upload light data to GPU buffer:
					for (int i = 0; i < activeLightCount; i++)
					{
						lightSourceDataBuffer[i] = cameraLightBuffer[i].GetLightSourceData();
					}
					ReadOnlySpan<Light.LightSourceData> lightSourceDataSpan = new(lightSourceDataBuffer, 0, (int)activeLightCount);
					core.Device.UpdateBuffer(lightDataBuffer, 0, lightSourceDataSpan);

					// Get and update a constant buffer containing the camera's global scene data:
					if (!_camera.GetGlobalConstantBuffer(activeLightCount, false, out DeviceBuffer? globalConstantBuffer) || globalConstantBuffer == null)
					{
						Logger.LogError("Failed to get or create camera's global constant buffer!");
						return false;
					}

					// Clear out all renderer lists for the upcoming frame:
					foreach (RendererList rendererList in rendererLists)
					{
						rendererList.Clear();
					}
					VisibleRendererCount = 0;
					SkippedRendererCount = 0;

					// No nodes and no scene behaviours? Skip drawing altogether:
					if (_renderers.Count == 0 && _scene.SceneBehaviourCount == 0)
					{
						return true;
					}

					// Assign each renderer to the most appropriate rendering list:
					foreach (IRenderer renderer in _renderers)
					{
						if (renderer.IsVisible && (renderer.LayerFlags & _camera.layerMask) != 0)
						{
							// Skip any renderers that cannot be mapped to any of the supported modes:
							if (!GetRendererListForMode(renderer.RenderMode, out RendererList? rendererList))
							{
								SkippedRendererCount++;
								continue;
							}

							// Add the renderer to its mode's corresponding list:
							rendererList!.renderers.Add(renderer);

							VisibleRendererCount++;
						}
					}

					if (VisibleRendererCount != 0)
					{
						bool success = true;

						// Issue draw calls for each renderer list:
						success &= DrawOpaqueRendererList(_camera);
						success &= DrawZSortedRendererList(_camera);
						success &= DrawUiRendererList(_camera);

						if (!success)
						{
							return false;
						}
					}
				}

				cameraLightBuffer.Clear();
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"An exception was caught while trying to draw scene using graphics stack of type '{nameof(ForwardPlusLightsStack)}'!", ex);
				return false;
			}
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

		private bool DrawOpaqueRendererList(Camera _camera)
		{
			if (!GetRendererListForMode(RenderMode.Opaque, out RendererList? opaqueList) || opaqueList == null)
			{
				return false;
			}

			if (opaqueList.renderers.Count == 0)
			{
				return true;
			}

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

			success &= _camera.BeginFrame(opaqueList.cmdList, true, out GraphicsDrawContext ctx);

			// Draw list of renderers as-is:
			foreach (IRenderer renderer in opaqueList.renderers)
			{
				FailedRendererCount += renderer.Draw(ctx) ? 0 : 1;
			}

			success &= _camera.EndFrame(opaqueList.cmdList);

			return success;
		}

		private bool DrawZSortedRendererList(Camera _camera)
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
			Vector3 viewportPosition = _camera.node.WorldPosition;
			Vector3 cameraDirection = Vector3.Transform(Vector3.UnitZ, _camera.node.WorldRotation);

			zSortedList.renderers.Sort((a, b) => a.GetZSortingDepth(viewportPosition, cameraDirection).CompareTo(b.GetZSortingDepth(viewportPosition, cameraDirection)));
			
			success &= _camera.BeginFrame(zSortedList.cmdList, false, out GraphicsDrawContext ctx);

			// Draw Z-sorted list of renderers:
			foreach (IRenderer renderer in zSortedList.renderers)
			{
				FailedRendererCount += renderer.Draw(ctx) ? 0 : 1;
			}

			success &= _camera.EndFrame(zSortedList.cmdList);

			return success;
		}

		private bool DrawUiRendererList(Camera _camera)
		{
			if (!GetRendererListForMode(RenderMode.UI, out RendererList? uiList) || uiList == null)
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

			success &= _camera.BeginFrame(uiList.cmdList, false, out GraphicsDrawContext ctx);

			// Draw list of renderers in strictly hierarchical order:
			foreach (IRenderer renderer in uiList.renderers)
			{
				FailedRendererCount += renderer.Draw(ctx) ? 0 : 1;
			}

			success &= _camera.EndFrame(uiList.cmdList);

			return success;
		}

		#endregion
	}
}
