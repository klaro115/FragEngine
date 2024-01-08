using System.Numerics;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Cameras;

public sealed class CameraSettings
{
	#region Fields

	public Matrix4x4? mtxWorld = null;

	public CameraOutput output = new();
	public CameraProjection projection = new();
	public CameraClearing clearing = new();

	#endregion
	#region Properties

	// WORLD:

	public Pose WorldPose
	{
		get => mtxWorld != null ? new(mtxWorld.Value) : Pose.Identity;
		set => mtxWorld = value.Matrix;
	}
	public Matrix4x4? MtxWorld
	{
		get => mtxWorld;
		set => mtxWorld = value;
	}
	public Matrix4x4 MtxCamera2World
	{
		get => mtxWorld != null && Matrix4x4.Invert(mtxWorld.Value, out Matrix4x4 mtxWorld2Camera) ? mtxWorld2Camera : Matrix4x4.Identity;
		set
		{
			if (Matrix4x4.Invert(value, out Matrix4x4 newMtxWorld))
			{
				mtxWorld = newMtxWorld;
			}
			else if (mtxWorld != null)
			{
				mtxWorld = Matrix4x4.Identity;
			}
		}
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

	public PixelFormat? ColorFormat
	{
		get => output.colorFormat;
		set => output.colorFormat = value;
	}
	public PixelFormat? DepthFormat
	{
		get => output.depthFormat;
		set => output.depthFormat = value;
	}
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
		get => projection.FieldOfViewRadians;
		set => projection.FieldOfViewRadians = value;
	}
	public float FieldOfViewDegrees
	{
		get => projection.FieldOfViewDegrees;
		set => projection.FieldOfViewDegrees = value;
	}
	public float OrthographicSize
	{
		get => projection.orthographicSize;
		set => projection.orthographicSize = Math.Clamp(value, 0.001f, 1000.0f);
	}

	public bool MirrorY
	{
		get => projection.mirrorY;
		set => projection.mirrorY = value;
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
