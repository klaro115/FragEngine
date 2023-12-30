using System.Numerics;
using FragEngine3.Containers;
using FragEngine3.EngineCore;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Cameras;

public sealed class CameraInstance : IDisposable
{
	#region Constructors

	public CameraInstance(GraphicsCore _graphicsCore)
	{
		graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore));
		hasOwnershipOfFramebuffer = true;
	}
	public CameraInstance(GraphicsCore _graphicsCore, Framebuffer _framebuffer, bool _hasOwnershipOfFramebuffer)
	{
		graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore));
		framebuffer = _framebuffer ?? throw new ArgumentNullException(nameof(_framebuffer));
		outputDesc = framebuffer.OutputDescription;
		hasOwnershipOfFramebuffer = _hasOwnershipOfFramebuffer;
	}

	~CameraInstance()
	{
		Dispose(false);
	}

	#endregion
	#region Fields
	
	public readonly GraphicsCore graphicsCore;
	public readonly bool hasOwnershipOfFramebuffer;

	private uint instanceVersion = 1;
	private bool isDrawing = false;

	private Framebuffer? framebuffer = null;
	private OutputDescription outputDesc = default;

	private VersionedMember<Matrix4x4> mtxInvWorld = new(Matrix4x4.Identity, 0);
	private VersionedMember<CameraOutput> output = new(new(), 0);
	private VersionedMember<CameraProjection> projection = new(new(), 0);
	private VersionedMember<CameraClearing> clearing = new(new(), 0);

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsInitialized => !IsDisposed && framebuffer != null && !framebuffer.IsDisposed;
	public bool IsDrawing => IsInitialized && isDrawing;
	private Logger Logger => graphicsCore.graphicsSystem.engine.Logger;

	// WORLD:

	public Pose WorldPose
	{
		get => new(MtxWorld);
		set => MtxWorld = value.Matrix;
	}
	public Matrix4x4 MtxWorld
	{
		get => Matrix4x4.Invert(mtxInvWorld.Value, out Matrix4x4 mtxWorld) ? mtxWorld : Matrix4x4.Identity;
		set
		{
			if (!Matrix4x4.Invert(value, out Matrix4x4 mtxInvValue))
			{
				mtxInvValue = Matrix4x4.Identity;
			}
			mtxInvWorld.UpdateValue(mtxInvWorld.Version + 1, mtxInvValue);
		}
	}
	public Matrix4x4 MtxInvWorld
	{
		get => mtxInvWorld.Value;
		set => mtxInvWorld.UpdateValue(mtxInvWorld.Version + 1, value);
	}

	// OUTPUT:

	public uint ResolutionX
	{
		get => output.Value.resolutionX;
		set
		{
			if (output.Value.resolutionX != value)
			{
				output.Value.resolutionX = Math.Max(value, 1);
				output.UpdateValue(output.Version + 1, output.Value);
			}
		}
	}
	public uint ResolutionY
	{
		get => output.Value.resolutionY;
		set
		{
			if (output.Value.resolutionY != value)
			{
				output.Value.resolutionY = Math.Max(value, 1);
				output.UpdateValue(output.Version + 1, output.Value);
			}
		}
	}
	public float AspectRatio => output.Value.AspectRatio;

	public PixelFormat ColorFormat => output.Value.colorFormat;
	public PixelFormat DepthFormat => output.Value.depthFormat;

	// PROJECTION:

	public CameraProjectionType ProjectionType
	{
		get => projection.Value.projectionType;
		set
		{
			if (projection.Value.projectionType != value)
			{
				projection.Value.projectionType = value;
				projection.UpdateValue(projection.Version + 1, projection.Value);
			}
		}
	}

	public float NearClipPlane
	{
		get => projection.Value.nearClipPlane;
		set
		{
			projection.Value.nearClipPlane = value;
			projection.UpdateValue(projection.Version + 1, projection.Value);
		}
	}
	public float FarClipPlane
	{
		get => projection.Value.farClipPlane;
		set
		{
			projection.Value.farClipPlane = value;
			projection.UpdateValue(projection.Version + 1, projection.Value);
		}
	}
	public float FieldOfViewRadians
	{
		get => projection.Value.fieldOfViewRad;
		set
		{
			projection.Value.fieldOfViewRad = Math.Clamp(value, 0.001f, MathF.PI);
			projection.UpdateValue(projection.Version + 1, projection.Value);
		}
	}
	public float FieldOfViewDegrees
	{
		get => projection.Value.fieldOfViewRad * (180.0f / MathF.PI);
		set
		{
			projection.Value.fieldOfViewRad = Math.Clamp(value, 0.01f, 179.99f) * MathF.PI / 180.0f;
			projection.UpdateValue(projection.Version + 1, projection.Value);
		}
	}
	public float OrthographicSize
	{
		get => projection.Value.othographicSize;
		set
		{
			projection.Value.othographicSize = Math.Clamp(value, 0.001f, 1000.0f);
			projection.UpdateValue(projection.Version + 1, projection.Value);
		}
	}

	public Matrix4x4 MtxWorld2Pixel
	{
		get
		{
			if (projection.Version < instanceVersion)
			{
				projection.Value.RecalculateClipSpaceMatrices(output.Value.AspectRatio);
				projection.UpdateValue(instanceVersion, projection.Value);
			}
			return projection.Value.mtxWorld2Pixel;
		}
	}
	public Matrix4x4 MtxPixel2World
	{
		get
		{
			if (projection.Version < instanceVersion)
			{
				projection.Value.RecalculateClipSpaceMatrices(output.Value.AspectRatio);
				projection.UpdateValue(instanceVersion, projection.Value);
			}
			return projection.Value.mtxPixel2World;
		}
	}

	// CLEARING:

	public bool ClearColor
	{
		get => clearing.Value.clearColor;
		set
		{
			if (clearing.Value.clearColor != value)
			{
				clearing.Value.clearColor = value;
				clearing.UpdateValue(clearing.Version + 1, clearing.Value);
			}
		}
	}
	public bool ClearDepth
	{
		get => clearing.Value.clearDepth;
		set
		{
			if (clearing.Value.clearDepth != value)
			{
				clearing.Value.clearDepth = value;
				clearing.UpdateValue(clearing.Version + 1, clearing.Value);
			}
		}
	}
	public bool ClearStencil
	{
		get => clearing.Value.clearStencil;
		set
		{
			if (clearing.Value.clearStencil != value)
			{
				clearing.Value.clearStencil = value;
				clearing.UpdateValue(clearing.Version + 1, clearing.Value);
			}
		}
	}

	public RgbaFloat ClearColorValue
	{
		get => clearing.Value.clearColorValue;
		set
		{
			clearing.Value.clearColorValue = value;
			clearing.UpdateValue(clearing.Version + 1, clearing.Value);
		}
	}
	public float ClearDepthValue
	{
		get => clearing.Value.clearDepthValue;
		set
		{
			clearing.Value.clearDepthValue = Math.Clamp(value, 0.0f, 1.0f);
			clearing.UpdateValue(clearing.Version + 1, clearing.Value);
		}
	}
	public byte ClearStencilValue
	{
		get => clearing.Value.clearStencilValue;
		set
		{
			clearing.Value.clearStencilValue = value;
			clearing.UpdateValue(clearing.Version + 1, clearing.Value);
		}
	}

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _)
	{
		IsDisposed = true;

		if (hasOwnershipOfFramebuffer)
		{
			framebuffer?.Dispose();
		}
	}

	public bool GetOrCreateFramebuffer(out Framebuffer _outFramebuffer, bool _forceRecreate = false)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot get framebuffer of disposed camera instance!");
			_outFramebuffer = null!;
			return false;
		}

		if (_forceRecreate || framebuffer == null || framebuffer.IsDisposed)
		{
			if (!hasOwnershipOfFramebuffer)
			{
				Logger.LogError("Externally owned framebuffer of camera instance has been disposed!");
				_outFramebuffer = null!;
				return false;
			}

			if (!graphicsCore.CreateRenderTargets(
				output.Value.colorFormat,
				output.Value.resolutionX,
				output.Value.resolutionY,
				output.Value.hasDepth,
				out _,
				out _,
				out framebuffer))
			{
				Logger.LogError("Failed to (re)create framebuffer for camera instance!");
				_outFramebuffer = null!;
				return false;
			}
			outputDesc = framebuffer.OutputDescription;
		}

		_outFramebuffer = framebuffer;
		return true;
	}

	public bool BeginDrawing(CommandList _cmdList, out Matrix4x4 _outMtxWorld2Clip)
	{
		if (_cmdList == null || _cmdList.IsDisposed)
		{
			Logger.LogError("Cannot begin frame on camera instance using null or disposed command list!");
			_outMtxWorld2Clip = Matrix4x4.Identity;
			return false;
		}

		// Check for out-of-date members:
		uint newInstanceVersion = instanceVersion;
		{
			uint maxVersion = instanceVersion;
			maxVersion = Math.Max(maxVersion, mtxInvWorld.Version);
			maxVersion = Math.Max(maxVersion, output.Version);
			maxVersion = Math.Max(maxVersion, projection.Version);
			maxVersion = Math.Max(maxVersion, clearing.Version);

			if (maxVersion != instanceVersion)
			{
				newInstanceVersion = maxVersion + 1;
			}
		}

		// Update values:
		bool hasMtxInvWorldChanged = mtxInvWorld.Version != newInstanceVersion;
		bool hasOutputChanged = output.Version != newInstanceVersion;
		bool hasProjectionChanged = projection.Version != newInstanceVersion;
		bool hasClearingChanged = clearing.Version != newInstanceVersion;

		if (hasMtxInvWorldChanged)
		{
			mtxInvWorld.UpdateValue(newInstanceVersion, mtxInvWorld.Value);
		}
		if (hasOutputChanged)
		{
			output.UpdateValue(newInstanceVersion, output.Value);
		}
		if (hasProjectionChanged)
		{
			projection.Value.RecalculateAllMatrices(mtxInvWorld.Value, output.Value.resolutionY, output.Value.resolutionY);
			projection.UpdateValue(newInstanceVersion, projection.Value);
		}
		else
		{
			if (hasOutputChanged)
			{
				projection.Value.RecalculateClipSpaceMatrices(mtxInvWorld.Value, output.Value.AspectRatio);
				projection.UpdateValue(newInstanceVersion, projection.Value);
			}
			if (hasMtxInvWorldChanged)
			{
				projection.Value.RecalculatePixelSpaceMatrices(output.Value.resolutionY, output.Value.resolutionY);
				projection.UpdateValue(newInstanceVersion, projection.Value);
			}
		}
		if (hasClearingChanged)
		{
			clearing.UpdateValue(newInstanceVersion, clearing.Value);
		}

		// Mark instance version up-to-date:
		instanceVersion = newInstanceVersion;

		// Output the most up-to-date projection matrix:
		_outMtxWorld2Clip = projection.Value.mtxWorld2Clip;

		// Get or recreate framebuffer: (always recreate if output resolution or format has changed)
		if (!GetOrCreateFramebuffer(out _, hasOutputChanged))
		{
			Logger.LogError("Failed to acquire render targets for camera instance, cannot begin drawing!");
			_outMtxWorld2Clip = Matrix4x4.Identity;
			return false;
		}

		// Assign render targets and set target areas:
		_cmdList.SetFramebuffer(framebuffer);
		_cmdList.SetFullViewports();
		_cmdList.SetFullScissorRects();

		// Clear render targets, if requested:
		if (ClearColor)
		{
			_cmdList.ClearColorTarget(0, ClearColorValue);
		}
		if (ClearDepth && output.Value.hasDepth)
		{
			if (ClearStencil && output.Value.hasStencil)
			{
				_cmdList.ClearDepthStencil(ClearDepthValue, ClearStencilValue);
			}
			else
			{
				_cmdList.ClearDepthStencil(ClearDepthValue);
			}
		}

		isDrawing = true;
		return true;
	}

	public bool EndDrawing()
	{
		if (!IsDrawing)
		{
			Logger.LogError("Cannot end drawing on camera instance that has not begun drawing!");
			return false;
		}

		//...
		return true;
	}

	#endregion
}
