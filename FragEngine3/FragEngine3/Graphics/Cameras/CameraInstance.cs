using System.Numerics;
using FragEngine3.EngineCore;
using FragEngine3.Scenes;
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

		projection.mirrorY = graphicsCore.DefaultMirrorY;

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

	private Matrix4x4 mtxWorld = Matrix4x4.Identity;
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
			mtxWorld = mtxWorld,
			output = output,
			projection = projection,
			clearing = clearing,
		};
		set
		{
			if (value.mtxWorld is not null)
			{
				mtxWorld = value.mtxWorld.Value;
				MarkDirty(DirtyFlags.Transformation);
			}
			output = value.output;
			projection = value.projection;
			clearing = value.clearing;
			MarkDirty(DirtyFlags.Output | DirtyFlags.Projection | DirtyFlags.Clearing);
		}
	}

	public Matrix4x4 MtxWorld
	{
		get => mtxWorld;
		set
		{
			mtxWorld = value;
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

	private Logger Logger => graphicsCore.graphicsSystem.Engine.Logger;

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

	/// <summary>
	/// Marks all of the camera's settings and resources as dirty, to force them updating prior to the next draw call.
	/// </summary>
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

		if (_forceRecreate || framebuffer is null || framebuffer.IsDisposed)
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

	/// <summary>
	/// Assign a set of render targets to this camera via a framebuffer that is not owned by the camera instance.
	/// This can be used to draw multiple render passes to different textures for compositing, or to just keep ownership of the render targets elsewhere.
	/// </summary>
	/// <param name="_newOverrideFramebuffer">A framebuffer that shall be drawn to. If null, any previously assigned override framebuffer will unset.</param>
	/// <param name="_adjustParamsIfOverrideMismatched">Whether to allow and adjust camera settings to the new framebuffer. If true, the output of the camera
	/// will be changed to support the override target, if false, attempting to assign an incompatible override target will fail and an error thrown.</param>
	/// <returns>True if the new framebuffer was set, or if the previous render targets were dropped, false on failure to assign the override target.</returns>
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
		if (_newOverrideFramebuffer is null || _newOverrideFramebuffer == framebuffer)
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

	/// <summary>
	/// Checks whether a given framebuffer if compatible with the camera's current settings.
	/// </summary>
	/// <param name="_framebuffer">A set of render targets that shall be checked against the instance's settings.</param>
	/// <returns>True if the given framebuffer may be used by this camera instance as-is, false otherwise.</returns>
	public bool IsFramebufferCompatible(in Framebuffer _framebuffer)
	{
		return !output.HaveSettingsChanged(in _framebuffer);
	}

	/// <summary>
	/// Begin rendering using this camera. This will bind the camera instance's framebuffer and projection setting to the graphics pipeline for all subsequent draw calls.
	/// </summary>
	/// <param name="_cmdList">A command list through which subsequent draw calls will be issued.</param>
	/// <param name="_clearRenderTargets">Whether to clear the current framebuffer's target textures (color and depth) before draw calls are issued.<para/>
	/// NOTE: Color targets will only be cleared if <see cref="CameraClearing.clearColor"/> is true, and depth targets will only be cleared if <see cref="CameraClearing.clearDepth"/>
	/// is true. This parameter merely serves as an override to disable all clearing for an upcoming batch of draw calls. When in doubt, set this to true.</param>
	/// <param name="_allowRecalculateMatrices">Whether to recalculate projection matrices for the upcoming batch of draw calls. If false, matrix values that were calculated
	/// in previous passes and frames will be reused. This should be true for the very first pass rendered with this camera instance, at the very least. If no settings have changed
	/// and no we're rendering multiple passes from a same viewpoint, this may be set to false, to skip unnecessary recalculations.</param>
	/// <param name="_outMtxWorld2Clip">Outputs the projection matrix that will be used throughout the upcoming batch of draw calls. Transforms from world space to clip space.</param>
	/// <returns>True if a render pass using this camera instance was successfully started, false otherwise.</returns>
	public bool BeginDrawing(CommandList _cmdList, bool _clearRenderTargets, bool _allowRecalculateMatrices, out Matrix4x4 _outMtxWorld2Clip)
	{
		if (_cmdList is null || _cmdList.IsDisposed)
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

		// Recalculate projection matrices: (each frame by default, because both parameters and pose can change)
		if (_allowRecalculateMatrices)
		{
			projection.RecalculateAllMatrices(in mtxWorld, output.resolutionX, output.resolutionY);
		}

		// Output the most up-to-date projection matrix:
		_outMtxWorld2Clip = projection.mtxWorld2Clip;

		// Get or recreate framebuffer: (always recreate if output resolution or format has changed)
		Framebuffer activeFramebuffer;
		if (HasOverrideFramebuffer)
		{
			activeFramebuffer = overrideFramebuffer!;
		}
		else if (!GetOrCreateFramebuffer(out activeFramebuffer, dirtyFlags.HasFlag(DirtyFlags.Output) && hasOwnershipOfFramebuffer))
		{
			Logger.LogError("Failed to acquire render targets for camera instance, cannot begin drawing!");
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

	/// <summary>
	/// Transforms a position from world space to a pixel coordinate on the camera's render target.
	/// </summary>
	/// <param name="_worldPoint">A position in world space.</param>
	/// <param name="_recalculateMatrices">Whether to recalculate projection matrices before transforming the point. When transforming a large number of
	/// points in immediate succession and without touching the camera in-between, this can be set to false for all but the very first call.</param>
	/// <returns>A coordinate in pixel space on the camera's render target.</returns>
	public Vector3 TransformWorldPosition2PixelCoordinate(Vector3 _worldPoint, bool _recalculateMatrices)
	{
		if (_recalculateMatrices)
		{
			projection.RecalculateAllMatrices(in mtxWorld, output.resolutionX, output.resolutionY);
		}
		return Vector3.Transform(_worldPoint, projection.mtxWorld2Pixel);
	}

	/// <summary>
	/// Transforms a pixel coordinate on the camera's render target to a position in world space.
	/// </summary>
	/// <param name="_screenCoord">A pixel coordinate (XY-axes) on the current render target. The Z-axis codes a depth value in the range [0..1].</param>
	/// <param name="_recalculateMatrices">Whether to recalculate projection matrices before transforming the point. When transforming a large number of
	/// points in immediate succession and without touching the camera in-between, this can be set to false for all but the very first call.</param>
	/// <returns>A position in world space.</returns>
	public Vector3 TransformPixelCoordinate2WorldPosition(Vector3 _screenCoord, bool _recalculateMatrices)
	{
		if (_recalculateMatrices)
		{
			projection.RecalculateAllMatrices(in mtxWorld, output.resolutionX, output.resolutionY);
		}
		return Vector3.Transform(_screenCoord, projection.mtxPixel2World);
	}

	public AABB CalculateViewportFrustumBounds()
	{
		Vector3 origin = mtxWorld.Translation;
		AABB bounds = new(origin, origin);

		//TODO [CRITICAL]: Calculate corner point positions of the projection frustum!

		return bounds;
	}

	#endregion
}
