using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
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

		private sealed class RendererList(int _initialCapacity) : IDisposable		// TODO: Move this to camera if possible. Having to juggle and reassign these, while pushing renderers in the correct lists is too slow right now!
		{
			~RendererList()
			{
				Dispose(false);
			}

			public readonly List<IRenderer> renderersOpaque = new(_initialCapacity);
			public readonly List<IRenderer> renderersTransparent = new(_initialCapacity);
			public readonly List<IRenderer> renderersUI = new(_initialCapacity);
			public CommandList? cmdList = null;

			public bool HasRenderers => renderersOpaque.Count != 0 || renderersTransparent.Count != 0;

			public void Dispose()
			{
				GC.SuppressFinalize(this);
				Dispose(true);
			}
			private void Dispose(bool _disposing)
			{
				cmdList?.Dispose();

				if (_disposing)
				{
					cmdList = null;
					Clear();
				}
			}

			public void Clear()
			{
				renderersOpaque.Clear();
				renderersTransparent.Clear();
				renderersUI.Clear();
			}
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

		private readonly Stack<RendererList> rendererListPool = new(5);
		private readonly Stack<RendererList> rendererListBusyStack = new(5);

		private readonly List<Light> cameraLightBuffer = new(64);
		private Light.LightSourceData[] lightSourceDataBuffer = new Light.LightSourceData[32];

		private CameraInstance? shadowMapCamera = null;
		private Texture? emptyShadowMapArray = null;

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

			emptyShadowMapArray?.Dispose();
			compositionResourceSet?.Dispose();

			foreach (RendererList rendererList in rendererListPool)
			{
				rendererList.Dispose();
			}
			foreach (RendererList rendererList in rendererListBusyStack)
			{
				rendererList.Dispose();
			}
			rendererListPool.Clear();
			rendererListBusyStack.Clear();
		}

		public bool Initialize(Scene _scene)
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
			if (IsInitialized)
			{
				Logger.LogError("Cannot re-initialize graphics stack that has already been initialized; shut it down first!");
				return false;
			}

			Scene = _scene;

			VisibleRendererCount = 0;
			SkippedRendererCount = 0;
			FailedRendererCount = 0;

			//...

			// OUTPUT COMPOSITION:

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

			// SHADOW MAPS:

			if (emptyShadowMapArray == null || emptyShadowMapArray.IsDisposed)
			{
				core.CreateShadowMapArray(8, 8, 1, out emptyShadowMapArray);
				// ^Note: The first shadow map texture is always a maximum-depth, no-shadows empty
				// texture that's used as a fallback for missing or wrongly assigned shadow maps.
				// This 'empty' placeholder array is populated with only a tiny 8x8 placeholder.
			}
			if (shadowMapCamera == null || shadowMapCamera.IsDisposed)
			{
				if (!CameraInstance.CreateShadowMapCamera(core, out shadowMapCamera))
				{
					Logger.LogError($"Failed to create shadow map camera for graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene.Name}'.");
					return false;
				}
			}

			Logger.LogMessage($"Initialized graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene.Name}'.");

			isDrawing = false;
			isInitialized = true;
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

				compositionResourceSet?.Dispose();

				cameraLightBuffer.Clear();

				foreach (RendererList rendererList in rendererListPool)
				{
					rendererList.Dispose();
				}
				foreach (RendererList rendererList in rendererListBusyStack)
				{
					rendererList.Dispose();
				}
				rendererListPool.Clear();
				rendererListBusyStack.Clear();

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

			// Return renderer used list to pool for re-use this frame:
			foreach (RendererList rendererList in rendererListBusyStack)
			{
				rendererListPool.Push(rendererList);
			}
			rendererListBusyStack.Clear();

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

				success &= TryRenderCamera(_scene, _renderers, camera, maxActiveLightCount, out uint activeLightCount);
			
				// Composite results:
				success &= CompositeFinalOutput(camera, activeLightCount);
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

		private bool GetRendererList(out RendererList _outRendererList)
		{
			if (!rendererListPool.TryPop(out RendererList? rendererList))
			{
				rendererList = new(32);
			}
			_outRendererList = rendererList;

			if (_outRendererList.cmdList == null || _outRendererList.cmdList.IsDisposed)
			{
				if (!_core.CreateCommandList(out _outRendererList.cmdList) || _outRendererList.cmdList == null)
				{
					Logger.LogError($"Failed to create command list for renderer list!");
					_outRendererList.Dispose();
					return false;
				}
				_outRendererList.cmdList.Name = $"CmdList_{nameof(ForwardPlusLightsStack)}";
			}

			_outRendererList.Clear();
			return true;
		}

		private bool TryRenderCamera(Scene _scene, List<IRenderer> _renderers, Camera _camera, uint _maxActiveLightCount, out uint _outActiveLightCount)
		{
			try
			{
				lock (lockObj)
				{
					// Get or create CPU and CPU-side buffers for assembling and binding light data:
					_outActiveLightCount = Math.Clamp((uint)cameraLightBuffer.Count, 0, _maxActiveLightCount);
					if (!_camera.GetLightDataBuffer(_outActiveLightCount, out DeviceBuffer? lightDataBuffer) || lightDataBuffer == null)
					{
						Logger.LogError("Failed to get or create camera's light source data buffer!");
						return false;
					}
					if (lightSourceDataBuffer.Length < _outActiveLightCount)
					{
						lightSourceDataBuffer = new Light.LightSourceData[_outActiveLightCount];
					}
					// Upload light data to GPU buffer:
					for (int i = 0; i < _outActiveLightCount; i++)
					{
						lightSourceDataBuffer[i] = cameraLightBuffer[i].GetLightSourceData();
					}
					ReadOnlySpan<Light.LightSourceData> lightSourceDataSpan = new(lightSourceDataBuffer, 0, (int)_outActiveLightCount);
					core.Device.UpdateBuffer(lightDataBuffer, 0, lightSourceDataSpan);

					GetRendererList(out RendererList rendererList);
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
							List<IRenderer>? modeList = renderer.RenderMode switch
							{
								RenderMode.Opaque =>		rendererList.renderersOpaque,
								RenderMode.Transparent =>	rendererList.renderersTransparent,
								RenderMode.UI =>			rendererList.renderersUI,
								_ =>						null,
							};
							if (modeList != null)
							{
								// Add the renderer to its mode's corresponding list:
								modeList.Add(renderer);
								VisibleRendererCount++;
							}
							else
							{
								SkippedRendererCount++;
							}
						}
					}

					rendererList.cmdList!.Begin();

					if (VisibleRendererCount != 0)
					{
						bool success = true;

						// Issue draw calls for each renderer list:
						success &= DrawRenderers(_camera, rendererList.renderersOpaque,			rendererList.cmdList!, RenderMode.Opaque,		_outActiveLightCount,	true, false);
						success &= DrawRenderers(_camera, rendererList.renderersTransparent,	rendererList.cmdList!, RenderMode.Transparent,	_outActiveLightCount,	false, true);
						success &= DrawRenderers(_camera, rendererList.renderersUI,				rendererList.cmdList!, RenderMode.UI,			0,						false, false);
						if (!success)
						{
							return false;
						}
					}
					else
					{
						// If no renderers, just allow clearing of render targets, to ensure backbuffer contents aren't undefined:
						_camera.BeginFrame(rendererList.cmdList!, emptyShadowMapArray!, RenderMode.Opaque, true, 0, out _);
						_camera.EndFrame(rendererList.cmdList!);
					}

					core.CommitCommandList(rendererList.cmdList);
					rendererList.cmdList.End();

					// Clear renderer list and return it to pool for later re-use:
					rendererList.Clear();
					rendererListBusyStack.Push(rendererList);
				}

				cameraLightBuffer.Clear();
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"An exception was caught while trying to draw scene using graphics stack of type '{nameof(ForwardPlusLightsStack)}'!", ex);
				_outActiveLightCount = 0;
				return false;
			}
		}

		private bool DrawRenderers(Camera _camera, List<IRenderer> _renderers, CommandList _cmdList, RenderMode _renderMode, uint _activeLightCount, bool _clearRenderTargets, bool _useZSorting)
		{
			bool success = true;

			// If required, sort all renderers by their Z-depth: (aka distance to camera)
			if (_useZSorting)
			{
				Vector3 viewportPosition = _camera.node.WorldPosition;
				Vector3 cameraDirection = Vector3.Transform(Vector3.UnitZ, _camera.node.WorldRotation);

				_renderers.Sort((a, b) => a.GetZSortingDepth(viewportPosition, cameraDirection).CompareTo(b.GetZSortingDepth(viewportPosition, cameraDirection)));
			}

			success &= _camera.BeginFrame(_cmdList, emptyShadowMapArray!, _renderMode, _clearRenderTargets, _activeLightCount, out CameraContext cameraCtx);

			// Draw list of renderers:
			if (_renderers.Count != 0)
			{
				foreach (IRenderer renderer in _renderers)
				{
					FailedRendererCount += renderer.Draw(cameraCtx) ? 0 : 1;
				}
			}

			success &= _camera.EndFrame(_cmdList);

			return success;
		}

		private bool CompositeFinalOutput(Camera _camera, uint _maxActiveLightCount)
		{
			if (!GetRendererList(out RendererList? rendererList) || rendererList?.cmdList == null)
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
				if (!_camera.SetOverrideCameraTarget(backbuffer, false))
				{
					Logger.LogError("Failed to set override render targets for graphics stack's composition pass!");
					return false;
				}
			}

			rendererList.cmdList.Begin();
			if (!_camera.BeginFrame(rendererList.cmdList, emptyShadowMapArray!, RenderMode.Custom, true, _maxActiveLightCount, out CameraContext cameraCtx))
			{
				Logger.LogError("Failed to begin frame on graphics stack's composition pass!");
				_camera.SetOverrideCameraTarget(null);
				return false;
			}

			bool success = true;

			// Create resource set containing all render targets that were previously drawn to:
			ResourceLayout resourceLayout = compositionMaterial.BoundResourceLayout!;
			if (resourceLayout != null && (compositionResourceSet == null || compositionResourceSet.IsDisposed))
			{
				success &= _camera.GetOrCreateCameraTarget(RenderMode.Opaque, out CameraTarget? opaqueTarget);
				success &= _camera.GetOrCreateCameraTarget(RenderMode.Transparent, out CameraTarget? transparentTarget);
				success &= _camera.GetOrCreateCameraTarget(RenderMode.UI, out CameraTarget? uiTarget);
				if (!success)
				{
					Logger.LogError("Failed to get camera's targets needed for output composition!");
					return false;
				}

				try
				{
					Texture texNull = core.graphicsSystem.TexPlaceholderTransparent.GetResource<TextureResource>(false, false)!.Texture!;

					BindableResource[] resources =
					[
						opaqueTarget?.texColorTarget ?? texNull,
						opaqueTarget?.texDepthTarget ?? texNull,
						transparentTarget?.texColorTarget ?? texNull,
						transparentTarget?.texDepthTarget ?? texNull,
						uiTarget?.texColorTarget ?? texNull,
					];
					ResourceSetDescription resourceSetDesc = new(resourceLayout, resources);

					compositionResourceSet = core.MainFactory.CreateResourceSet(ref resourceSetDesc);
					compositionResourceSet.Name = $"ResSet_Bound_{RESOURCE_KEY_COMPOSITION_MATERIAL}";
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
			success &= compositionRenderer.Draw(cameraCtx);

			// Finish drawing and submit command list to GPU:
			success &= _camera.EndFrame(rendererList.cmdList);
			rendererList.cmdList.End();

			core.CommitCommandList(rendererList.cmdList);

			// Reset camera state:
			_camera.SetOverrideCameraTarget(null);

			// Return renderer list for re-use:
			rendererList.Clear();
			rendererListBusyStack.Push(rendererList);
			return success;
		}

		#endregion
	}
}
