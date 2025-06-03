using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Scenes;

namespace FragEngine3.Graphics.Stack.Default;

public sealed class DefaultGraphicsStack : IGraphicsStack
{
	#region Constructors

	public DefaultGraphicsStack(GraphicsCore _graphicsCore)
	{
		Core = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Graphics core may not be null!");
		logger = _graphicsCore.graphicsSystem.Engine.Logger;

		resources = new(_graphicsCore);
		shadowMapStack = new(_graphicsCore, resources);
		sceneRenderStack = new(_graphicsCore);
		postProcessingStack = new();
		compositionStack = new(_graphicsCore);
	}

	#endregion
	#region Fields

	private readonly Logger logger;

	private readonly DefaultStackResources resources;
	private readonly DefaultStackShadowMaps shadowMapStack;
	private readonly DefaultStackSceneRender sceneRenderStack;
	private readonly DefaultStackPostProcessing postProcessingStack;
	private readonly DefaultStackComposition compositionStack;

	private Scene? lastDrawnScene = null;

	private bool isInitialized = false;
	private bool isDrawing = false;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsValid => !IsDisposed;
	public bool IsInitialized => !IsDisposed && isInitialized;
	public bool IsDrawing => IsInitialized && isDrawing;

	public GraphicsCore Core { get; }

	#endregion
	#region Methods

	public void Dispose()
	{
		IsDisposed = true;

		shadowMapStack.Dispose();
		compositionStack.Dispose();
		resources.Dispose();
	}

	public bool Initialize(Scene _scene)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot initialize disposed graphics stack!");
			return false;
		}
		if (IsInitialized)
		{
			logger.LogError("Cannot re-initialize graphics stack that is already initialized!");
			return false;
		}

		if (!resources.Initialize())
		{
			return false;
		}

		if (!compositionStack.Initialize(_scene))
		{
			return false;
		}

		isInitialized = true;
		return true;
	}

	public void Shutdown()
	{
		if (IsDrawing)
		{
			EndDrawing();
		}

		shadowMapStack.Shutdown();
		compositionStack.Shutdown();
		resources.Shutdown();

		isInitialized = false;
	}

	public bool Reset()
	{
		Shutdown();

		bool success = Initialize(lastDrawnScene!);

		sceneRenderStack.Reset();
		return success;
	}

	public bool DrawStack(Scene _scene, List<IRenderer> _renderers, in IList<CameraComponent> _cameras, in IList<ILightSource> _lights)
	{
		if (!IsInitialized)
		{
			logger.LogError("Cannot draw uninitialized graphics stack!");
			return false;
		}
		if (isDrawing)
		{
			logger.LogError("Graphics stack is already drawing!");
			return false;
		}
		if (_scene is null || _scene.IsDisposed)
		{
			logger.LogError("Cannot draw null or disposed scene!");
			return false;
		}

		if (!StartDrawing(_scene))
		{
			logger.LogError("Failed to start drawing graphics stack!");
			return false;
		}

		bool success = true;

		// Shadow maps:
		success &= shadowMapStack.DrawShadowMaps(_scene, in _renderers, in _cameras, in _lights, out uint lightCount, out uint lightCountShadowMapped);

		// Scene render:
		SceneContext? sceneCtx = null;
		if (success)
		{
			success &= resources.CreateSceneContext(_scene, lightCount, lightCountShadowMapped, 0, out sceneCtx);
		}
		bool rebuildResSetCamera = false;
		if (success)
		{		
			success &= sceneRenderStack.DrawAllSceneCameras(in sceneCtx!, /* _scene, */ in _renderers, in _cameras, in _lights, lightCount, lightCountShadowMapped, out rebuildResSetCamera);
		}

		// Scene composition:
		if (success)
		{
			success &= compositionStack.CompositeSceneOutput(in sceneCtx!, in _cameras, rebuildResSetCamera);
		}

		// Scene post-processing:
		if (success)
		{
			success &= postProcessingStack.ApplyScenePostProcessing();
		}

		// Output composition:
		if (success)
		{
			success &= compositionStack.CompositeFinalOutput(in sceneCtx!, in _cameras, rebuildResSetCamera);
		}

		if (!EndDrawing())
		{
			logger.LogError("Failed to end drawing graphics stack!");
			success = false;
		}
		return success;
	}

	private bool StartDrawing(Scene _scene)
	{
		isDrawing = true;
		lastDrawnScene = _scene;

		return true;
	}

	private bool EndDrawing()
	{
		isDrawing = false;
		return true;
	}

	#endregion
}
