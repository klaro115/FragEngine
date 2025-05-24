using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting.Internal;
using FragEngine3.Graphics.Utility;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Stack.Default;

internal sealed class DefaultStackResources(GraphicsCore _graphicsCore) : IDisposable
{
	#region Constructors

	~DefaultStackResources()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly GraphicsCore graphicsCore = _graphicsCore;
	private readonly Logger logger = _graphicsCore.graphicsSystem.Engine.Logger;

	private bool isInitialized = false;

	private ResourceLayout? resLayoutCamera = null;
	private ResourceLayout? resLayoutObject = null;

	private DeviceBuffer? cbScene = null;
	private LightDataBuffer? dummyLightDataBuffer = null;
	private ShadowMapArray? shadowMapArray = null;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsInitialized => !IsDisposed && isInitialized;

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
		Shutdown();
	}

	public bool Initialize()
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot initialize disposed graphics stack resources!");
			return false;
		}
		if (isInitialized)
		{
			logger.LogError("Cannot re-initialize graphics stack resources that are already initialized!");
			return false;
		}

		if (!CameraUtility.CreateCameraResourceLayout(graphicsCore, out resLayoutCamera))
		{
			logger.LogError("Failed to create default camera resource layout for graphics stack!");
			return false;
		}
		if (!SceneUtility.CreateObjectResourceLayout(graphicsCore, out resLayoutObject))
		{
			logger.LogError("Failed to create default object resource layout for graphics stack!");
			return false;
		}
		if (!CameraUtility.CreateConstantBuffer_CBScene(graphicsCore, out cbScene))
		{
			logger.LogError("Failed to create scene constant buffer for graphics stack!");
			return false;
		}

		try
		{
			dummyLightDataBuffer = new(graphicsCore);
			shadowMapArray = new(graphicsCore, 3);
		}
		catch (Exception ex)
		{
			logger.LogException("Failed to create dummy light data buffer and shadow map array for graphics stack!", ex);
			return false;
		}

		isInitialized = true;
		return true;
	}

	public void Shutdown()
	{
		resLayoutCamera?.Dispose();
		resLayoutObject?.Dispose();

		cbScene?.Dispose();
		dummyLightDataBuffer?.Dispose();
		shadowMapArray?.Dispose();

		resLayoutCamera = null;
		resLayoutObject = null;

		cbScene = null;
		dummyLightDataBuffer = null;
		shadowMapArray = null;

		isInitialized = false;
	}

	public bool CreateSceneContext(Scene _scene, uint _lightCount, uint _lightCountShadowMapped, ushort _sceneResourceVersion, out SceneContext? _outSceneCtx)
	{
		if (!IsInitialized)
		{
			logger.LogError("Cannot create scene context from uninitialized graphics stack resources!");
			_outSceneCtx = null;
			return false;
		}

		_outSceneCtx = new(_lightCount, _lightCountShadowMapped)
		{
			Scene = _scene,

			ResLayoutCamera = resLayoutCamera!,
			ResLayoutObject = resLayoutObject!,
			CbScene = cbScene!,
			DummyLightDataBuffer = dummyLightDataBuffer!,
			ShadowMapArray = shadowMapArray!,
			SceneResourceVersion = _sceneResourceVersion,
		};
		return true;
	}

	#endregion
}
