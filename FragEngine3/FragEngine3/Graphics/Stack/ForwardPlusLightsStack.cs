using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Utility;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Stack
{
	public sealed class ForwardPlusLightsStack(GraphicsCore _core) : IGraphicsStack
	{
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

		// Lists & object management:
		private readonly List<Camera> activeCameras = new(2);
		private readonly List<Light> activeLights = new(10);
		private readonly List<Light> activeLightsShadowMapped = new(5);
		private LightSourceData[] activeLightData = [];

		private readonly List<IRenderer> activeRenderersOpaque = new(128);
		private readonly List<IRenderer> activeRenderersTransparent = new(128);
		private readonly List<IRenderer> activeRenderersUI = new(128);
		private readonly List<IRenderer> activeShadowCasters = new(128);

		// Global resources:
		private DeviceBuffer? cbScene = null;
		private readonly Stack<CommandList> commandListPool = new();
		private readonly Stack<CommandList> commandListsInUse = new();
		private ResourceLayout? resLayoutCamera = null;
		private ResourceLayout? resLayoutObject = null;

		// Shadow maps:
		private Texture? texShadowMaps = null;
		private uint texShadowMapsCapacity = 0;
		private DeviceBuffer? dummyBufLights = null;
		private Sampler? samplerShadowMaps = null;

		// Output composition:
		private ResourceSet? compositionResourceSet = null;
		private StaticMeshRenderer? compositionRenderer = null;

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
		public GraphicsCore Core => core;

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

			cbScene?.Dispose();
			resLayoutCamera?.Dispose();
			resLayoutObject?.Dispose();
			compositionResourceSet?.Dispose();
			dummyBufLights?.Dispose();
			texShadowMaps?.Dispose();
			texShadowMapsCapacity = 0;
			samplerShadowMaps?.Dispose();

			foreach (CommandList cmdList in commandListPool)
			{
				cmdList.Dispose();
			}
			foreach (CommandList cmdList in commandListsInUse)
			{
				cmdList.Dispose();
			}
			commandListPool.Clear();
			commandListsInUse.Clear();
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

			// GLOBAL RESOURCES:

			if (!CameraUtility.CreateCameraResourceLayout(in core, out resLayoutCamera))
			{
				Logger.LogError("Failed to create default camera resource layout for graphics stack!");
				return false;
			}

			if (!SceneUtility.CreateObjectResourceLayout(in core, out resLayoutObject))
			{
				Logger.LogError("Failed to create default object resource layout for graphics stack!");
				return false;
			}

			// SHADOW MAPPING:

			if (dummyBufLights == null || dummyBufLights.IsDisposed)
			{
				if (!CameraUtility.CreateOrResizeLightDataBuffer(in core, 1, ref dummyBufLights, out _))
				{
					Logger.LogError("Failed to create dummy light data buffer for graphics stack!");
					return false;
				}
			}

			if (texShadowMaps == null || texShadowMaps.IsDisposed || texShadowMapsCapacity == 0)
			{
				const uint shadowResolution = ShadowMapUtility.shadowResolution;
				if (!ShadowMapUtility.CreateShadowMapArray(in core, shadowResolution, shadowResolution, 1, out texShadowMaps))
				{
					Logger.LogError("Failed to create initial shadow map texture array for graphics stack!");
					return false;
				}
				texShadowMapsCapacity = 1;
			}

			if (samplerShadowMaps == null || samplerShadowMaps.IsDisposed)
			{
				if (!ShadowMapUtility.CreateShadowSampler(in core, out samplerShadowMaps))
				{
					Logger.LogError("Failed to create shadow map sampler for graphics stack!");
					return false;
				}
			}

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
					verticesExt =
					[
						new ExtendedVertex(Vector3.UnitY, new(0, 0)),
						new ExtendedVertex(Vector3.UnitY, new(1, 0)),
						new ExtendedVertex(Vector3.UnitY, new(0, 1)),
						new ExtendedVertex(Vector3.UnitY, new(1, 1)),
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
			
			Logger.LogMessage($"Initialized graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene.Name}'.");

			isDrawing = false;
			isInitialized = true;
			return true;
		}

		public void Shutdown()
		{
			if (!isInitialized) return;

			if (Scene != null && compositionRenderer != null)
			{
				Scene.rootNode.DestroyChild(compositionRenderer.node);
				compositionRenderer = null;
			}

			activeCameras.Clear();
			activeLights.Clear();
			activeLightsShadowMapped.Clear();
			activeRenderersOpaque.Clear();
			activeRenderersTransparent.Clear();
			activeRenderersUI.Clear();
			activeShadowCasters.Clear();

			cbScene?.Dispose();
			resLayoutCamera?.Dispose();
			resLayoutObject?.Dispose();
			compositionResourceSet?.Dispose();
			dummyBufLights?.Dispose();
			texShadowMaps?.Dispose();
			texShadowMapsCapacity = 0;
			samplerShadowMaps?.Dispose();

			foreach (CommandList cmdList in commandListPool)
			{
				cmdList.Dispose();
			}
			foreach (CommandList cmdList in commandListsInUse)
			{
				cmdList.Dispose();
			}
			commandListPool.Clear();
			commandListsInUse.Clear();

			VisibleRendererCount = 0;
			SkippedRendererCount = 0;
			FailedRendererCount = 0;

			Scene = null;

			isDrawing = false;
			isInitialized = false;

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

		private bool GetOrCreateCommandList(out CommandList _outCmdList)
		{
			if (commandListPool.TryPop(out CommandList? cmdList) && cmdList != null)
			{
				_outCmdList = cmdList;
			}
			else
			{
				_outCmdList = core.MainFactory.CreateCommandList();
			}
			commandListsInUse.Push(_outCmdList);
			return true;
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

			// Prepare global resources for drawing the scene and sort out non-visible objects:
			success &= BeginDrawScene(
				in _renderers,
				in _cameras,
				in _lights,
				maxActiveLightCount,
				out SceneContext sceneCtx,
				out Vector3 renderFocalPoint,
				out float renderFocalRadius,
				out bool rebuildResSetCamera,
				out bool texShadowMapsHasChanged);
			if (!success)
			{
				Logger.LogError("Graphics stack failed to begin drawing scene!");
				return false;
			}

			// Skip rendering if no cameras are active:
			if (activeCameras.Count == 0)
			{
				VisibleRendererCount = 0;
				SkippedRendererCount = 0;
				isDrawing = false;
				return true;
			}

			// Draw shadow maps for all shadow-casting light sources:
			success &= DrawShadowMaps(
				in sceneCtx,
				renderFocalPoint,
				renderFocalRadius,
				maxActiveLightCount,
				rebuildResSetCamera,
				texShadowMapsHasChanged,
				out uint shadowMapLightCount);

			// Recreate updated scene context if values changed between passes:
			if (shadowMapLightCount != sceneCtx.lightCountShadowMapped)
			{
				sceneCtx = new(
					Scene,
					resLayoutCamera!,
					resLayoutObject!,
					cbScene!,
					texShadowMaps!,
					samplerShadowMaps!,
					sceneCtx.lightCount,
					shadowMapLightCount);
			}

			// Draw each active camera component in the scene, and composite output:
			success &= DrawSceneCameras(
				in sceneCtx,
				maxActiveLightCount,
				rebuildResSetCamera);

			isDrawing = false;
			return success;
		}

		private bool BeginDrawScene(
			in List<IRenderer> _renderers,
			in IList<Camera> _cameras,
			in IList<Light> _lights,
			uint _maxActiveLightCount,
			out SceneContext _outSceneCtx,
			out Vector3 _outRenderFocalPoint,
			out float _outRenderFocalRadius,
			out bool _outRebuildResSetCamera,
			out bool _outTexShadowsHasChanged)
		{
			// Clear all lists for new frame:
			activeCameras.Clear();
			activeLights.Clear();
			activeLightsShadowMapped.Clear();
			activeRenderersOpaque.Clear();
			activeRenderersTransparent.Clear();
			activeRenderersUI.Clear();
			activeShadowCasters.Clear();

			// Identify only active cameras and visible light sources:
			foreach (Camera camera in _cameras)
			{
				if (!camera.IsDisposed && camera.layerMask != 0 && camera.node.IsEnabledInHierarchy())
				{
					activeCameras.Add(camera);
				}
			}

			foreach (Light light in _lights)
			{
				// Skip disabled and overly dim light sources:
				if (light.IsDisposed || light.layerMask == 0 || light.LightIntensity < 0.0001f || !light.node.IsEnabledInHierarchy())
					continue;

				// Only retain sources whose light may be seen by an active camera:
				foreach (Camera camera in activeCameras)
				{
					if (light.CheckVisibilityByCamera(in camera))
					{
						activeLights.Add(light);
						break;
					}
				}
			}
			// Sort lights, to prioritize shadow casters first, and higher priority lights second:
			activeLights.Sort(Light.CompareLightsForSorting);
			foreach (Light light in activeLights)
			{
				if (light.CastShadows)
				{
					activeLightsShadowMapped.Add(light);
				}
			}

			// Identify only visible renderers:
			foreach (IRenderer renderer in _renderers)
			{
				if (renderer.IsVisible)
				{
					//TODO [later]: We'll just take all renderers for now, but they need to be excluded/culled if not visible by any camera.

					List<IRenderer>? rendererList = renderer.RenderMode switch
					{
						RenderMode.Opaque => activeRenderersOpaque,
						RenderMode.Transparent => activeRenderersTransparent,
						RenderMode.UI => activeRenderersUI,
						_ => null,
					};
					rendererList?.Add(renderer);
				}
			}
			activeShadowCasters.AddRange(activeRenderersOpaque);
			activeShadowCasters.AddRange(activeRenderersTransparent);

			// Return command lists used in last frame to pool:
			foreach (CommandList cmdList in commandListsInUse)
			{
				commandListPool.Push(cmdList);
			}
			commandListsInUse.Clear();

			// Identify (main) camera focal point:
			{
				Camera focalCamera = Camera.MainCamera != null && Camera.MainCamera.node.scene == Scene
					? Camera.MainCamera
					: _cameras[0];

				float cameraFarClipPlane = focalCamera.ProjectionSettings.farClipPlane;
				Pose cameraWorldPose = focalCamera.node.WorldTransformation;
				_outRenderFocalRadius = ShadowMapUtility.directionalLightSize;
				_outRenderFocalPoint = cameraWorldPose.position;
			}

			// Update scene constant buffer:
			if (!CameraUtility.UpdateConstantBuffer_CBScene(
				in core,
				in Scene!.settings,
				ref cbScene,
				out _outRebuildResSetCamera))
			{
				Logger.LogError("Failed to create or update scene constant buffer!");
				_outSceneCtx = null!;
				_outTexShadowsHasChanged = false;
				return false;
			}

			// Resize shadow map texture array to reflect maximum number of shadow-casting lights:
			_outTexShadowsHasChanged = false;
			uint lightCountShadowMapped = Math.Min((uint)activeLightsShadowMapped.Count, _maxActiveLightCount);
			if (texShadowMaps == null || texShadowMaps.IsDisposed || texShadowMapsCapacity < lightCountShadowMapped)
			{
				_outRebuildResSetCamera = true;
				_outTexShadowsHasChanged = true;
				texShadowMaps?.Dispose();

				const uint shadowResolution = ShadowMapUtility.shadowResolution;
				if (!ShadowMapUtility.CreateShadowMapArray(
					in core,
					shadowResolution,
					shadowResolution,
					lightCountShadowMapped,
					out texShadowMaps))
				{
					Logger.LogError("Failed to create shadow map texture array for graphics stack!");
					_outSceneCtx = null!;
					_outTexShadowsHasChanged = false;
					return false;
				}
				texShadowMapsCapacity = lightCountShadowMapped;
			}

			_outSceneCtx = new(
				Scene,
				resLayoutCamera!,
				resLayoutObject!,
				cbScene!,
				texShadowMaps!,
				samplerShadowMaps!,
				(uint)activeLights.Count,
				(uint)activeLightsShadowMapped.Count);
			return true;
		}
		
		private bool DrawShadowMaps(
			in SceneContext _sceneCtx,
			Vector3 _renderFocalPoint,
			float _renderFocalRadius,
			uint _maxActiveLightCount,
			bool _rebuildResSetCamera,
			bool _texShadowsHasChanged,
			out uint _outShadowMapLightCount)
		{
			// No visible shadow-casting light? We're done here:
			_outShadowMapLightCount = 0;
			if (activeLightsShadowMapped.Count == 0)
			{
				return true;
			}

			// Fetch or create a command list for shadow rendering:
			if (!GetOrCreateCommandList(out CommandList cmdList))
			{
				return false;
			}
			cmdList.Begin();

			bool success = true;

			int shadowMappedLightCount = Math.Min(activeLightsShadowMapped.Count, (int)_maxActiveLightCount);

			try
			{
				for (int i = 0; i < shadowMappedLightCount; ++i)
				{
					Light light = activeLightsShadowMapped[i];
					success &= light.BeginDrawShadowMap(
						in _sceneCtx,
						in cmdList,
						in dummyBufLights!,
						_renderFocalPoint,
						_renderFocalRadius,
						_outShadowMapLightCount,
						out CameraPassContext lightCtx,
						_rebuildResSetCamera,
						_texShadowsHasChanged);
					if (!success) break;

					//TODO [later]: Exclude renderers that are entirely outside of point/spot lights' maximum range.

					// Draw renderers for opaque and tranparent geometry, ignore UI:
					foreach (IRenderer renderer in activeShadowCasters)
					{
						if ((light.layerMask & renderer.LayerFlags) != 0)
						{
							success &= renderer.DrawShadowMap(_sceneCtx, lightCtx);
						}
					}

					success &= light.EndDrawShadowMap();
					_outShadowMapLightCount++;
				}
			}
			catch (Exception ex)
			{
				Logger.LogException($"An unhandled exception was caught while drawing shadow maps, around shadow map index {_outShadowMapLightCount}!", ex);
				success = false;
			}

			// If any shadows maps were rendered, submit command list for execution:
			cmdList.End();
			if (_outShadowMapLightCount != 0)
			{
				success &= core.CommitCommandList(cmdList);
			}
			return success;
		}

		private bool DrawSceneCameras(in SceneContext _sceneCtx, uint _maxActiveLightCount, bool _rebuildAllResSetCamera)
		{
			bool success = true;

			// Gather light data for each active light source:
			if (activeLightData.Length < activeLights.Count)
			{
				activeLightData = new LightSourceData[activeLights.Count];
			}
			for (int i = 0; i < activeLights.Count; ++i)
			{
				activeLightData[i] = activeLights[i].GetLightSourceData();
			}

			// Fetch or create a command list for shadow rendering:
			if (!GetOrCreateCommandList(out CommandList cmdList))
			{
				return false;
			}
			cmdList.Begin();

			for (uint i = 0; i < activeCameras.Count; ++i)
			{
				try
				{
					//TODO [later]: Pre-filter lights to only include those are actually visible by current camera!

					Camera camera = activeCameras[(int)i];
					uint activeLightCount = (uint)activeLights.Count;
					if (!camera.GetLightDataBuffer(activeLightCount, out DeviceBuffer? lightDataBuffer, out bool bufLightsChanged))
					{
						success = false;
						continue;
					}
					bool rebuildResSetCamera = _rebuildAllResSetCamera || bufLightsChanged;

					if (!camera.BeginFrame(activeLightCount, false, out _))
					{
						success = false;
						continue;
					}

					bool result = true;

					result &= camera.SetOverrideCameraTarget(null);
					result &= CameraUtility.UpdateLightDataBuffer(in core, in lightDataBuffer!, in activeLightData, activeLightCount, _maxActiveLightCount);

					result &= DrawSceneRenderers(in _sceneCtx, cmdList, camera, RenderMode.Opaque, activeRenderersOpaque, true, rebuildResSetCamera, i);
					result &= DrawSceneRenderers(in _sceneCtx, cmdList, camera, RenderMode.Transparent, activeRenderersTransparent, false, rebuildResSetCamera, i);
					result &= DrawSceneRenderers(in _sceneCtx, cmdList, camera, RenderMode.UI, activeRenderersUI, false, rebuildResSetCamera, i);

					if (result)
					{
						result &= CompositeFinalOutput(in _sceneCtx, camera, rebuildResSetCamera, i);
					}
					result &= camera.EndFrame();
					success &= result;
				}
				catch (Exception ex)
				{
					Logger.LogException($"An unhandled exception was caught while drawing scene camera {i}!", ex);
					success = false;
					break;
				}
			}

			// If any shadows maps were rendered, submit command list for execution:
			cmdList.End();
			success &= core.CommitCommandList(cmdList);

			return success;
		}

		private bool DrawSceneRenderers(
			in SceneContext _sceneCtx,
			in CommandList _cmdList,
			Camera _camera,
			RenderMode _renderMode,
			in List<IRenderer> _renderers,
			bool _clearRenderTargets,
			bool _rebuildResSetCamera,
			uint _cameraIdx)
		{
			if (!_camera.BeginPass(
				in _sceneCtx,
				_cmdList,
				_renderMode,
				_clearRenderTargets,
				_cameraIdx,
				(uint)activeLights.Count,
				(uint)activeLightsShadowMapped.Count,
				out CameraPassContext cameraPassCtx,
				_rebuildResSetCamera))
			{
				return false;
			}

			bool success = true;

			foreach (IRenderer renderer in _renderers)
			{
				if ((_camera.layerMask & renderer.LayerFlags) != 0)
				{
					success &= renderer.Draw(_sceneCtx, cameraPassCtx);
				}
			}

			success &= _camera.EndPass();
			return success;
		}

		private bool CompositeFinalOutput(in SceneContext _sceneCtx, Camera _camera, bool _rebuildResSetCamera, uint _cameraIdx)
		{
			if (!GetOrCreateCommandList(out CommandList cmdList))
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

			cmdList.Begin();
			if (!_camera.BeginPass(
				in _sceneCtx,
				cmdList,
				RenderMode.Custom,
				true,
				_cameraIdx,
				0, 0,
				out CameraPassContext cameraPassCtx,
				_rebuildResSetCamera))
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
				success &= _camera.GetOrCreateCameraTarget(RenderMode.Opaque,		out CameraTarget? opaqueTarget);
				success &= _camera.GetOrCreateCameraTarget(RenderMode.Transparent,	out CameraTarget? transparentTarget);
				success &= _camera.GetOrCreateCameraTarget(RenderMode.UI,			out CameraTarget? uiTarget);
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
			success &= compositionRenderer.Draw(_sceneCtx, cameraPassCtx);

			// Finish drawing and submit command list to GPU:
			success &= _camera.EndPass();
			cmdList.End();

			core.CommitCommandList(cmdList);

			// Reset camera state:
			_camera.SetOverrideCameraTarget(null);
			return success;
		}

		#endregion
	}
}
