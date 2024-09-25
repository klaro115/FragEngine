using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.ConstantBuffers;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Graphics.PostProcessing;
using FragEngine3.Graphics.Stack.ForwardPlusLights;
using FragEngine3.Graphics.Utility;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Stack;

public sealed class ForwardPlusLightsStack : IGraphicsStack		//TODO: This type is a convoluted and unmaintainable mess, needs intense cleaning up.
{
	#region Constructors

	public ForwardPlusLightsStack(GraphicsCore _core)
	{
		Core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

		shadowMaps = new(this, sceneObjects);
		composition = new(this);
		sceneRender = new(this, sceneObjects, composition);
	}

	~ForwardPlusLightsStack()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private bool isInitialized = false;
	private bool isDrawing = false;

	// Lists & object management:
	private readonly ForwardPlusLightsSceneObjects sceneObjects = new();
	private readonly ForwardPlusLightsSceneRender sceneRender;

	// Global resources:
	private DeviceBuffer? cbScene = null;
	private CBScene cbSceneData = default;
	private readonly Stack<CommandList> commandListPool = new();
	private readonly Stack<CommandList> commandListsInUse = new();
	private ResourceLayout? resLayoutCamera = null;
	private ResourceLayout? resLayoutObject = null;
	private ushort sceneResourceVersion = 0;

	// Shadow maps:
	private ForwardPlusLightsShadowMaps shadowMaps;

	// Output composition:
	private readonly ForwardPlusLightsComposition composition;

	// Postprocessing:
	private IPostProcessingStack? postProcessingStackScene = null;
	private IPostProcessingStack? postProcessingStackFinal = null;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsValid => !IsDisposed && Core.IsInitialized && Scene != null && !Scene.IsDisposed;	//TODO
	public bool IsInitialized => !IsDisposed && isInitialized;
	public bool IsDrawing => IsInitialized && isDrawing;

	public Scene? Scene { get; private set; } = null;
	public GraphicsCore Core { get; init; }

	public IPostProcessingStack? PostProcessingStackScene
	{
		get => postProcessingStackScene != null && !postProcessingStackScene.IsDisposed ? postProcessingStackScene : null;
		set { if (value == null || !value.IsDisposed) postProcessingStackScene = value; }
	}
	public IPostProcessingStack? PostProcessingStackFinal
	{
		get => postProcessingStackFinal != null && !postProcessingStackFinal.IsDisposed ? postProcessingStackFinal : null;
		set { if (value == null || !value.IsDisposed) postProcessingStackFinal = value; }
	}

	public int VisibleRendererCount { get; private set; } = 0;
	public int SkippedRendererCount { get; private set; } = 0;
	public int FailedRendererCount { get; private set; } = 0;

	private Logger Logger => Core.graphicsSystem.engine.Logger ?? Logger.Instance!;

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
		
		shadowMaps.Dispose();
		composition.Dispose();

		postProcessingStackScene?.Dispose();
		postProcessingStackFinal?.Dispose();

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

		if (!CameraUtility.CreateCameraResourceLayout(Core, out resLayoutCamera))
		{
			Logger.LogError("Failed to create default camera resource layout for graphics stack!");
			return false;
		}

		if (!SceneUtility.CreateObjectResourceLayout(Core, out resLayoutObject))
		{
			Logger.LogError("Failed to create default object resource layout for graphics stack!");
			return false;
		}

		// SHADOW MAPPING:

		if (shadowMaps is null || shadowMaps.IsDisposed)
		{
			shadowMaps = new(this, sceneObjects);
		}
		if (!shadowMaps.Initialize())
		{
			Logger.LogError("Failed to initialize graphics stack's shadow map module!");
			return false;
		}

		// OUTPUT COMPOSITION:

		if (!composition.Initialize())
		{
			Logger.LogError("Failed to initialize graphics stack's composition module!");
			return false;
		}
		
		Logger.LogMessage($"Initialized graphics stack of type '{nameof(ForwardPlusLightsStack)}' for scene '{Scene.Name}'.");

		isDrawing = false;
		isInitialized = true;
		return true;
	}

	public void Shutdown()
	{
		if (!isInitialized) return;

		composition.Shutdown();
		sceneObjects.Clear();

		cbScene?.Dispose();
		resLayoutCamera?.Dispose();
		resLayoutObject?.Dispose();
		
		shadowMaps.Dispose();

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
		if (!Initialize(Scene!))
		{
			return false;
		}

		bool success = true;

		// If available, refresh post-processing stacks:
		success &= postProcessingStackScene == null || postProcessingStackScene.Refresh();
		success &= postProcessingStackFinal == null || postProcessingStackFinal.Refresh();

		return success;
	}

	internal bool GetOrCreateCommandList(out CommandList _outCmdList)
	{
		if (commandListPool.TryPop(out CommandList? cmdList) && cmdList != null)
		{
			_outCmdList = cmdList;
		}
		else
		{
			_outCmdList = Core.MainFactory.CreateCommandList();
		}
		commandListsInUse.Push(_outCmdList);
		return true;
	}

	public bool DrawStack(Scene _scene, List<IRenderer> _renderers, in IList<Camera> _cameras, in IList<ILightSource> _lights)
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
		if (_renderers is null)
		{
			Logger.LogError("Cannot draw graphics stack for null list of node-renderers pairs!");
			return false;
		}
		if (_cameras is null)
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

		uint maxActiveLightCount = Math.Max(Core.graphicsSystem.Settings.MaxActiveLightCount, 1);

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
		if (sceneObjects.activeCameras.Count == 0)
		{
			VisibleRendererCount = 0;
			SkippedRendererCount = 0;
			isDrawing = false;
			return true;
		}

		// Draw shadow maps for all shadow-casting light sources:
		success &= shadowMaps.DrawShadowMaps(
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
			sceneCtx = new(sceneCtx.lightCount, shadowMapLightCount)
			{
				Scene = Scene,
				ResLayoutCamera = resLayoutCamera!,
				ResLayoutObject = resLayoutObject!,
				CbScene = cbScene!,
				DummyLightDataBuffer = shadowMaps.DummyLightDataBuffer!,
				ShadowMapArray = shadowMaps.ShadowMapArray!,
				SceneResourceVersion = sceneResourceVersion,
			};
		}

		// Draw each active camera component in the scene, and composite output:
		success &= sceneRender.DrawSceneCameras(in sceneCtx, rebuildResSetCamera);

		isDrawing = false;
		return success;
	}

	private bool BeginDrawScene(
		in List<IRenderer> _renderers,
		in IList<Camera> _cameras,
		in IList<ILightSource> _lights,
		uint _maxActiveLightCount,
		out SceneContext _outSceneCtx,
		out Vector3 _outRenderFocalPoint,
		out float _outRenderFocalRadius,
		out bool _outRebuildResSetCamera,
		out bool _outTexShadowsHasChanged)
	{
		// Return command lists used in last frame to pool:
		foreach (CommandList cmdList in commandListsInUse)
		{
			commandListPool.Push(cmdList);
		}
		commandListsInUse.Clear();

		// Clear all lists for new frame:
		sceneObjects.Clear();

		// Prepare and sort cameras, light sources, and renderers:
		sceneObjects.PrepareScene(
			in _renderers,
			in _cameras,
			in _lights);

		// Identify (main) camera focal point:
		{
			Camera focalCamera = Camera.MainCamera is not null && Camera.MainCamera.node.scene == Scene
				? Camera.MainCamera
				: _cameras[0];

			Pose cameraWorldPose = focalCamera.node.WorldTransformation;
			_outRenderFocalRadius = LightConstants.directionalLightSize;
			_outRenderFocalPoint = cameraWorldPose.position;
		}

		// Update scene constant buffer:
		if (!CameraUtility.UpdateConstantBuffer_CBScene(
			Core,
			in Scene!.settings,
			ref cbSceneData,
			ref cbScene,
			out _outRebuildResSetCamera))
		{
			Logger.LogError("Failed to create or update scene constant buffer!");
			_outSceneCtx = null!;
			_outTexShadowsHasChanged = false;
			return false;
		}
		if (_outRebuildResSetCamera)
		{
			sceneResourceVersion++;
		}

		// Resize shadow map texture array to reflect maximum number of shadow-casting lights:
		if (!shadowMaps.PrepareShadowMaps(
			_maxActiveLightCount,
			ref _outRebuildResSetCamera,
			ref sceneResourceVersion,
			out _outTexShadowsHasChanged))
		{
			_outSceneCtx = null!;
			return false;
		}

		_outSceneCtx = new(sceneObjects.ActiveLightCount, sceneObjects.ActiveShadowMappedLightsCount)
		{
			Scene = Scene,
			ResLayoutCamera = resLayoutCamera!,
			ResLayoutObject = resLayoutObject!,
			CbScene = cbScene!,
			DummyLightDataBuffer = shadowMaps.DummyLightDataBuffer!,
			ShadowMapArray = shadowMaps.ShadowMapArray!,
			SceneResourceVersion = sceneResourceVersion,
		};
		return true;
	}

	#endregion
}
