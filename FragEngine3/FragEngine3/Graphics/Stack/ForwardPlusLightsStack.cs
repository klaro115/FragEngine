using FragEngine3.Containers;
using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Components.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
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
		private uint stackVersion = 1;

		private static readonly int[] rendererModeIndices =
		[
			0,		// RenderMode.Compute
			1,		// RenderMode.Opaque
			2,		// RenderMode.Transparent
			-1,		// RenderMode.Volumetric
			-1,		// RenderMode.PostProcessing
			3,		// RenderMode.UI
			4,		// RenderMode.Custom
		];
		private readonly RendererList[] rendererLists =
		[
			new(RenderMode.Compute, 4),
			new(RenderMode.Opaque, 128),
			new(RenderMode.Transparent, 32),
			new(RenderMode.UI, 32),
			new(RenderMode.Custom, 1),
		];

		private readonly List<Light> cameraLightBuffer = new(64);
		private Light.LightSourceData[] lightSourceDataBuffer = new Light.LightSourceData[32];

		private VersionedMember<Pipeline> compositionPipeline = new(null!, 0);
		private ResourceSet? compositionResourceSet = null;
		private StaticMeshRenderer? compositionRenderer = null;

		private readonly object lockObj = new();

		#endregion
		#region Constants

		public const string RESOURCE_KEY_COMPOSITION_MATERIAL = "Mtl_ForwardPlusLight_Composition";
		public const string RESOURCE_KEY_FULLSCREEN_QUAD_MESH = "FullscreenQuad";
		public const string NODE_NAME_COMPOSITION_RENDERER = "ForwardPlusLight_Composition";
		public const uint COMPOSITION_LAYER_MASK = 0x800000u;

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

			compositionPipeline.DisposeValue();
			compositionResourceSet?.Dispose();
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

				stackVersion++;
				isDrawing = false;
				isInitialized = true;
			}

			if (!Scene.engine.ResourceManager.GetResource(RESOURCE_KEY_FULLSCREEN_QUAD_MESH, out ResourceHandle fullscreenQuadHandle))
			{
				// Create fullscreen quad:
				MeshSurfaceData fullscreenQuadData = new()
				{
					verticesBasic =
					[
						new BasicVertex(new(-1, -1, 0), new(0, 0, -1), new(0, 0)),
						new BasicVertex(new( 1, -1, 0), new(0, 0, -1), new(1, 0)),
						new BasicVertex(new(-1,  1, 0), new(0, 0, -1), new(0, 1)),
						new BasicVertex(new( 1,  1, 0), new(0, 0, -1), new(1, 1)),
					],
					indices16 =
					[
						0, 2, 1,
						2, 3, 1,
					],
				};
				StaticMesh fullscreenQuadMesh = new(RESOURCE_KEY_FULLSCREEN_QUAD_MESH, Scene.engine, false, out fullscreenQuadHandle);
				fullscreenQuadMesh.SetGeometry(in fullscreenQuadData);
			}

			if (!Scene.FindNode(NODE_NAME_COMPOSITION_RENDERER, out SceneNode? compositionNode))
			{
				compositionNode = Scene.rootNode.CreateChild(NODE_NAME_COMPOSITION_RENDERER);
				compositionNode.LocalTransformation = Pose.Identity;
				if (compositionNode.CreateComponent(out compositionRenderer) && compositionRenderer != null)
				{
					compositionRenderer.SetMaterial(RESOURCE_KEY_COMPOSITION_MATERIAL, true);
					compositionRenderer.SetMesh(RESOURCE_KEY_FULLSCREEN_QUAD_MESH);
					compositionRenderer.LayerFlags = COMPOSITION_LAYER_MASK;
				}
			}

			return true;
		}

		public void Shutdown()
		{
			if (!isInitialized) return;

			lock(lockObj)
			{
				if (Scene != null && compositionRenderer != null)
				{
					Scene.rootNode.DestroyChild(compositionRenderer.node);
					compositionRenderer = null;
				}

				compositionPipeline.DisposeValue();
				compositionResourceSet?.Dispose();

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
				stackVersion++;
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
			for (int i = 0; i < _cameras.Count; ++i)
			{
				// Skip any cameras that are expired or disabled:
				Camera camera = _cameras[i];
				if (camera == null || camera.IsDisposed || !camera.node.IsEnabledInHierarchy() || camera.layerMask == 0u)
				{
					continue;
				}

				GatherLightsForCamera(in _lights, camera);

				success &= TryRenderCamera(_scene, _renderers, camera, maxActiveLightCount);
			
				// Composite results:
				success &= CompositeFinalOutput(camera, maxActiveLightCount);
			}
			
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
							if (!GetRendererListForMode(renderer.RenderMode, out RendererList? rendererList, out _))
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
						success &= DrawOpaqueRendererList(_camera, activeLightCount);
						success &= DrawZSortedRendererList(_camera, activeLightCount);
						success &= DrawUiRendererList(_camera);

						if (!success)
						{
							return false;
						}
					}
					else
					{
						// If no renderers, just allow clearing of render targets, to ensure backbuffer contents aren't undefined:
						GetRendererListForMode(RenderMode.Opaque, out RendererList? opaqueList, out CommandList? cmdList);
						_camera.BeginFrame(cmdList!, RenderMode.Opaque, 0, true, out _, out _);
						_camera.EndFrame(cmdList!);
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

		private bool GetRendererListForMode(RenderMode _mode, out RendererList? _outRendererList, out CommandList? _outCmdList)
		{
			int modeIdx = (int)_mode;
			int rendererListIdx = rendererModeIndices[modeIdx];

			if (rendererListIdx >= 0)
			{
				_outRendererList = rendererLists[rendererListIdx];

				if (_outRendererList.cmdList == null || _outRendererList.cmdList.IsDisposed)
				{
					if (!_core.CreateCommandList(out _outRendererList.cmdList) || _outRendererList.cmdList == null)
					{
						Logger.LogError($"Failed to create command list for {_mode} renderer list!");
						_outRendererList.cmdList?.Dispose();
						_outRendererList.cmdList = null;
						_outCmdList = null;
						return false;
					}
					_outRendererList.cmdList.Name = $"CmdList_{_mode}";
				}

				_outCmdList = _outRendererList.cmdList;
				return true;
			}
			_outRendererList = null;
			_outCmdList = null;
			return false;
		}

		private bool DrawOpaqueRendererList(Camera _camera, uint _activeLightCount)
		{
			if (!GetRendererListForMode(RenderMode.Opaque, out RendererList? opaqueList, out CommandList? cmdList) || opaqueList == null || cmdList == null)
			{
				return false;
			}

			if (opaqueList.renderers.Count == 0)
			{
				return true;
			}

			bool success = true;

			success &= _camera.BeginFrame(cmdList, RenderMode.Opaque, _activeLightCount, true, out GraphicsDrawContext drawCtx, out CameraContext cameraCtx);

			// Draw list of renderers as-is:
			foreach (IRenderer renderer in opaqueList.renderers)
			{
				FailedRendererCount += renderer.Draw(drawCtx, cameraCtx) ? 0 : 1;
			}

			success &= _camera.EndFrame(cmdList);

			return success;
		}

		private bool DrawZSortedRendererList(Camera _camera, uint _activeLightCount)
		{
			if (!GetRendererListForMode(RenderMode.Transparent, out RendererList? zSortedList, out CommandList? cmdList) || zSortedList == null || cmdList == null)
			{
				return false;
			}

			if (zSortedList.renderers.Count == 0) return true;

			bool success = true;

			// Sort all transparent renderers by their Z-depth: (aka distance to camera)
			Vector3 viewportPosition = _camera.node.WorldPosition;
			Vector3 cameraDirection = Vector3.Transform(Vector3.UnitZ, _camera.node.WorldRotation);

			zSortedList.renderers.Sort((a, b) => a.GetZSortingDepth(viewportPosition, cameraDirection).CompareTo(b.GetZSortingDepth(viewportPosition, cameraDirection)));
			
			success &= _camera.BeginFrame(cmdList, RenderMode.Transparent, _activeLightCount, false, out GraphicsDrawContext drawCtx, out CameraContext cameraCtx);

			// Draw Z-sorted list of renderers:
			foreach (IRenderer renderer in zSortedList.renderers)
			{
				FailedRendererCount += renderer.Draw(drawCtx, cameraCtx) ? 0 : 1;
			}

			success &= _camera.EndFrame(cmdList);

			return success;
		}

		private bool DrawUiRendererList(Camera _camera)
		{
			if (!GetRendererListForMode(RenderMode.UI, out RendererList? uiList, out CommandList? cmdList) || uiList == null || cmdList == null)
			{
				return false;
			}

			if (uiList.renderers.Count == 0) return true;

			bool success = true;

			success &= _camera.BeginFrame(cmdList, RenderMode.UI, 0, false, out GraphicsDrawContext drawCtx, out CameraContext cameraCtx);

			// Draw list of renderers in strictly hierarchical order:
			foreach (IRenderer renderer in uiList.renderers)
			{
				FailedRendererCount += renderer.Draw(drawCtx, cameraCtx) ? 0 : 1;
			}

			success &= _camera.EndFrame(cmdList);

			return success;
		}

		private bool CompositeFinalOutput(Camera _camera, uint _maxActiveLightCount)
		{
			if (!GetRendererListForMode(RenderMode.Custom, out RendererList? customList, out CommandList? cmdList) || customList == null || cmdList == null)
			{
				return false;
			}
			if (compositionRenderer == null || compositionRenderer.IsDisposed)
			{
				return false;
			}
			Material compositionMaterial = (compositionRenderer.MaterialHandle!.GetResource(true, true) as Material)!;

			// If this is the main camera, ouput composited image directly to the swapchain's backbuffer:
			if (_camera.IsMainCamera)
			{
				Framebuffer backbuffer = core.Device.SwapchainFramebuffer;
				if (!_camera.SetOverrideRenderTargets(backbuffer, false))
				{
					Logger.LogError("Failed to set override render targets for graphics stack's composition pass!");
					return false;
				}
			}

			if (!_camera.BeginFrame(cmdList, RenderMode.Custom, _maxActiveLightCount, false, out GraphicsDrawContext drawCtx, out CameraContext cameraCtx))
			{
				Logger.LogError("Failed to begin frame on graphics stack's composition pass!");
				return false;
			}

			bool success = true;

			// Create resource set containing all render targets that were previously drawn to:
			ResourceLayout resourceLayout = compositionMaterial.BoundResourceLayout!;
			if (resourceLayout != null && (compositionResourceSet == null || compositionResourceSet.IsDisposed))
			{
				success &= _camera.GetOrCreateOwnRenderTargets(RenderMode.Opaque, out CameraTarget? opaqueTarget);
				success &= _camera.GetOrCreateOwnRenderTargets(RenderMode.Transparent, out CameraTarget? transparentTarget);
				success &= _camera.GetOrCreateOwnRenderTargets(RenderMode.UI, out CameraTarget? uiTarget);
				if (!success)
				{
					Logger.LogError("Failed to get camera's render targets needed for output composition!");
					return false;
				}

				try
				{
					BindableResource[] resources =
					[
						opaqueTarget?.texColorTarget!,
						opaqueTarget?.texDepthTarget!,
						transparentTarget?.texColorTarget!,
						transparentTarget?.texDepthTarget!,
						uiTarget?.texColorTarget!,
					];
					ResourceSetDescription resourceSetDesc = new(resourceLayout, resources);

					compositionResourceSet = core.MainFactory.CreateResourceSet(ref resourceSetDesc);
				}
				catch (Exception ex)
				{
					Logger.LogException("Failed to create resource set containing render targets for output composition!", ex, EngineCore.Logging.LogEntrySeverity.Major);
					return false;
				}
			}

			// Bind render targets:
			if (compositionResourceSet != null && !compositionRenderer.SetOverrideBoundResourceSet(compositionResourceSet))
			{
				Logger.LogError("Failed to override bound resource set for graphics stack's composition pass!");
				return false;
			}

			// Send draw calls for output composition:
			success &= compositionRenderer.Draw(drawCtx, cameraCtx);

			success &= _camera.EndFrame(cmdList);

			return success;
		}

		#endregion
	}
}
