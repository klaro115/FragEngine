using System.Numerics;
using FragEngine3.EngineCore;
using Veldrid;

namespace FragEngine3.Graphics.Cameras;

public sealed class CameraInstance : IDisposable
{
	#region Types

	[Flags]
	private enum DirtyFlags
	{
		Transformation	= 1,
		Output			= 2,
		Projection		= 4,
		Clearing		= 8,

		All				= Transformation | Output | Projection | Clearing,
	}

	#endregion
	#region Constructors

	public CameraInstance(GraphicsCore _graphicsCore, bool _createFramebufferImmediately = false)
	{
		graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore));
		hasOwnershipOfFramebuffer = true;

		if (_createFramebufferImmediately)
		{
			GetOrCreateFramebuffer(out _, true);
		}

		MarkDirty();
	}
	public CameraInstance(GraphicsCore _graphicsCore, Framebuffer _framebuffer, bool _hasOwnershipOfFramebuffer)
	{
		graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore));
		framebuffer = _framebuffer ?? throw new ArgumentNullException(nameof(_framebuffer));
		hasOwnershipOfFramebuffer = _hasOwnershipOfFramebuffer;

		MarkDirty();
	}

	~CameraInstance()
	{
		Dispose(false);
	}

	#endregion
	#region Fields
	
	public readonly GraphicsCore graphicsCore;
	public readonly bool hasOwnershipOfFramebuffer;

	private DirtyFlags dirtyFlags = DirtyFlags.All;
	private bool isDrawing = false;

	private Framebuffer? framebuffer = null;
	private Framebuffer? overrideFramebuffer = null;

	private Matrix4x4 mtxInvWorld = Matrix4x4.Identity;
	private CameraOutput output = new();
	private CameraProjection projection = new();
	private CameraClearing clearing = new();

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsInitialized => !IsDisposed && ((framebuffer != null && !framebuffer.IsDisposed) || HasOverrideFramebuffer);
	public bool IsDrawing => IsInitialized && isDrawing;
	public bool HasOverrideFramebuffer => overrideFramebuffer != null && !overrideFramebuffer.IsDisposed;

	public CameraSettings Settings
	{
		get => new()
		{
			mtxInvWorld = mtxInvWorld,
			output = output,
			projection = projection,
			clearing = clearing,
		};
		set
		{
			if (value.mtxInvWorld != null)
			{
				mtxInvWorld = value.mtxInvWorld.Value;
				MarkDirty(DirtyFlags.Transformation);
			}
			output = value.output;
			projection = value.projection;
			clearing = value.clearing;
			MarkDirty(DirtyFlags.Output | DirtyFlags.Projection | DirtyFlags.Clearing);
		}
	}

	public Matrix4x4 MtxInvWorld
	{
		get => mtxInvWorld;
		set
		{
			mtxInvWorld = value;
			MarkDirty(DirtyFlags.Transformation);
		}
	}
	public CameraOutput OutputSettings
	{
		get => output;
		set
		{
			output = value;
			MarkDirty(DirtyFlags.Output);
		}
	}
	public CameraProjection ProjectionSettings
	{
		get => projection;
		set
		{
			projection = value;
			MarkDirty(DirtyFlags.Projection);
		}
	}
	public CameraClearing ClearingSettings
	{
		get => clearing;
		set
		{
			clearing = value;
			MarkDirty(DirtyFlags.Clearing);
		}
	}

	private Logger Logger => graphicsCore.graphicsSystem.engine.Logger;

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

	public void MarkDirty()
	{
		dirtyFlags = DirtyFlags.All;
	}
	private void MarkDirty(DirtyFlags _dirtyFlags)
	{
		dirtyFlags |= _dirtyFlags;
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
				output.colorFormat ?? graphicsCore.DefaultColorTargetPixelFormat,
				output.resolutionX,
				output.resolutionY,
				output.hasDepth,
				out _,
				out _,
				out framebuffer))
			{
				Logger.LogError("Failed to (re)create framebuffer for camera instance!");
				_outFramebuffer = null!;
				return false;
			}
		}

		_outFramebuffer = framebuffer;
		return true;
	}

	public bool SetOverrideFramebuffer(Framebuffer? _newOverrideFramebuffer, bool _adjustParamsIfOverrideMismatched)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot assign override framebuffer on disposed camera instance!");
			return false;
		}
		if (isDrawing)
		{
			Logger.LogError("Cannot assign override framebuffer on a camera instance that is drawing!");
			return false;
		}

		// If null, reset override and draw to own targets instead:
		if (_newOverrideFramebuffer == null || _newOverrideFramebuffer == framebuffer)
		{
			overrideFramebuffer = null;
			return true;
		}
		if (_newOverrideFramebuffer.IsDisposed)
		{
			Logger.LogError("Cannot assign disposed framebuffer as override to camera instance!");
			return false;
		}

		// If the override's resolution and format mismatch current settings, either adjust those or throw an error:
		if (!IsFramebufferCompatible(in _newOverrideFramebuffer))
		{
			if (!_adjustParamsIfOverrideMismatched)
			{
				Logger.LogError("New override framebuffer mismatches output description of camera instance!");
				return false;
			}

			output.Update(in _newOverrideFramebuffer);

			MarkDirty(DirtyFlags.Output);
		}

		overrideFramebuffer = _newOverrideFramebuffer;
		return true;
	}

	public bool IsFramebufferCompatible(in Framebuffer _framebuffer)
	{
		return !output.HaveSettingsChanged(in _framebuffer);
	}

	public bool BeginDrawing(CommandList _cmdList, bool _clearRenderTargets, out Matrix4x4 _outMtxWorld2Clip)
	{
		if (_cmdList == null || _cmdList.IsDisposed)
		{
			Logger.LogError("Cannot begin frame on camera instance using null or disposed command list!");
			_outMtxWorld2Clip = Matrix4x4.Identity;
			return false;
		}
		if (IsDisposed)
		{
			Logger.LogError("Cannot begin frame on disposed camera instance!");
			_outMtxWorld2Clip = Matrix4x4.Identity;
			return false;
		}
		if (isDrawing)
		{
			Logger.LogError("Cannot begin frame on a camera instance that is already drawing!");
			_outMtxWorld2Clip = Matrix4x4.Identity;
			return false;
		}

		// Update values:
		if (dirtyFlags.HasFlag(DirtyFlags.Projection) ||
			dirtyFlags.HasFlag(DirtyFlags.Output) ||
			dirtyFlags.HasFlag(DirtyFlags.Transformation))
		{
			projection.RecalculateAllMatrices(mtxInvWorld, output.resolutionY, output.resolutionY);
		}

		// Output the most up-to-date projection matrix:
		_outMtxWorld2Clip = projection.mtxWorld2Clip;

		// Get or recreate framebuffer: (always recreate if output resolution or format has changed)
		Framebuffer activeFramebuffer;
		if (HasOverrideFramebuffer)
		{
			activeFramebuffer = overrideFramebuffer!;
		}
		else if (!GetOrCreateFramebuffer(out activeFramebuffer, dirtyFlags.HasFlag(DirtyFlags.Output)))
		{
			Logger.LogError("Failed to acquire render targets for camera instance, cannot begin drawing!");
			_outMtxWorld2Clip = Matrix4x4.Identity;
			return false;
		}

		// Assign render targets and set target areas:
		_cmdList.SetFramebuffer(activeFramebuffer);
		_cmdList.SetFullViewports();
		_cmdList.SetFullScissorRects();

		// Clear render targets, if requested:
		if (_clearRenderTargets)
		{
			if (clearing.clearColor)
			{
				_cmdList.ClearColorTarget(0, clearing.clearColorValue);
			}
			if (clearing.clearDepth && output.hasDepth)
			{
				if (clearing.clearStencil && output.hasStencil)
				{
					_cmdList.ClearDepthStencil(clearing.clearDepthValue, clearing.clearStencilValue);
				}
				else
				{
					_cmdList.ClearDepthStencil(clearing.clearDepthValue);
				}
			}
		}

		isDrawing = true;
		dirtyFlags = 0;
		return true;
	}

	public bool EndDrawing()
	{
		if (!IsDrawing)
		{
			Logger.LogError("Cannot end drawing on camera instance that has not begun drawing!");
			return false;
		}

		isDrawing = false;
		return true;
	}

	#endregion
}
