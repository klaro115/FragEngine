using System.Numerics;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Cameras;

public sealed class CameraSettings
{
	#region Fields

	public Matrix4x4? mtxInvWorld = null;

	public CameraOutput output = new();
	public CameraProjection projection = new();
	public CameraClearing clearing = new();

	#endregion
	#region Properties

	// WORLD:

	public Pose WorldPose
	{
		get => new(MtxWorld);
		set => MtxWorld = value.Matrix;
	}
	public Matrix4x4 MtxWorld
	{
		get => mtxInvWorld != null && Matrix4x4.Invert(mtxInvWorld.Value, out Matrix4x4 mtxWorld) ? mtxWorld : Matrix4x4.Identity;
		set
		{
			if (Matrix4x4.Invert(value, out Matrix4x4 mtxInvValue))
			{
				mtxInvWorld = mtxInvValue;
			}
			else if (mtxInvWorld != null)
			{
				mtxInvWorld = Matrix4x4.Identity;
			}
		}
	}
	public Matrix4x4? MtxInvWorld
	{
		get => mtxInvWorld;
		set => mtxInvWorld = value;
	}

	// OUTPUT:

	public uint ResolutionX
	{
		get => output.resolutionX;
		set => output.resolutionX = Math.Max(value, 1);
	}
	public uint ResolutionY
	{
		get => output.resolutionY;
		set => output.resolutionY = Math.Max(value, 1);
	}
	public float AspectRatio => output.AspectRatio;

	public PixelFormat ColorFormat => output.colorFormat;
	public PixelFormat DepthFormat => output.depthFormat;
	public bool HasDepth => output.hasDepth;

	// PROJECTION:

	public CameraProjectionType ProjectionType
	{
		get => projection.projectionType;
		set => projection.projectionType = value;
	}

	public float NearClipPlane
	{
		get => projection.nearClipPlane;
		set => projection.nearClipPlane = value;
	}
	public float FarClipPlane
	{
		get => projection.farClipPlane;
		set => projection.farClipPlane = value;
	}
	public float FieldOfViewRadians
	{
		get => projection.fieldOfViewRad;
		set => projection.fieldOfViewRad = Math.Clamp(value, 0.001f, MathF.PI);
	}
	public float FieldOfViewDegrees
	{
		get => projection.fieldOfViewRad * (180.0f / MathF.PI);
		set => projection.fieldOfViewRad = Math.Clamp(value, 0.01f, 179.99f) * MathF.PI / 180.0f;
	}
	public float OrthographicSize
	{
		get => projection.othographicSize;
		set => projection.othographicSize = Math.Clamp(value, 0.001f, 1000.0f);
	}

	public Matrix4x4 MtxWorld2Pixel
	{
		get
		{
			projection.RecalculateClipSpaceMatrices(output.AspectRatio);
			return projection.mtxWorld2Pixel;
		}
	}
	public Matrix4x4 MtxPixel2World
	{
		get
		{
			projection.RecalculateClipSpaceMatrices(output.AspectRatio);
			return projection.mtxPixel2World;
		}
	}

	// CLEARING:

	public bool ClearColor
	{
		get => clearing.clearColor;
		set => clearing.clearColor = value;
	}
	public bool ClearDepth
	{
		get => clearing.clearDepth;
		set => clearing.clearDepth = value;
	}
	public bool ClearStencil
	{
		get => clearing.clearStencil;
		set => clearing.clearStencil = value;
	}

	public RgbaFloat ClearColorValue
	{
		get => clearing.clearColorValue;
		set => clearing.clearColorValue = value;
	}
	public float ClearDepthValue
	{
		get => clearing.clearDepthValue;
		set => clearing.clearDepthValue = Math.Clamp(value, 0.0f, 1.0f);
	}
	public byte ClearStencilValue
	{
		get => clearing.clearStencilValue;
		set => clearing.clearStencilValue = value;
	}

	#endregion
}
