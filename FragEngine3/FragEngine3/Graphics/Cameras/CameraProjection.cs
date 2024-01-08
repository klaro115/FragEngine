using System.Numerics;

namespace FragEngine3.Graphics.Cameras;

public struct CameraProjection
{
	#region Constructors

	public CameraProjection() { }

	#endregion
	#region Fields

	public CameraProjectionType projectionType = CameraProjectionType.Perspective;
	public float nearClipPlane = 0.1f;
	public float farClipPlane = 1000.0f;
	private float fieldOfViewRad = DEFAULT_FOV_RAD;
	public float orthographicSize = 5.0f;
	public bool mirrorY = true;

	public Matrix4x4 mtxWorld2Camera = Matrix4x4.Identity;		// World space => Camera's local space
	public Matrix4x4 mtxWorld2Clip = Matrix4x4.Identity;		// World space => Clip space
	public Matrix4x4 mtxClip2Pixel = Matrix4x4.Identity;		// Clip space => Pixel space

	public Matrix4x4 mtxWorld2Pixel = Matrix4x4.Identity;		// World space => Pixel space
	public Matrix4x4 mtxPixel2World = Matrix4x4.Identity;       // Pixel space => World space

	#endregion
	#region Properties

	public float FieldOfViewRadians
	{
		readonly get => fieldOfViewRad;
		set => fieldOfViewRad = Math.Clamp(value, 0.001f * DEG2RAD, 179.9f * DEG2RAD);
	}
	public float FieldOfViewDegrees
	{
		readonly get => fieldOfViewRad * RAD2DEG;
		set => fieldOfViewRad = Math.Clamp(value, 0.001f, 179.9f) * DEG2RAD;
	}

	#endregion
	#region Constants

	private const float DEG2RAD = MathF.PI / 180.0f;
	private const float RAD2DEG = 180.0f / MathF.PI;

	public const float DEFAULT_FOV_DEG = 60.0f;
	public const float DEFAULT_FOV_RAD = DEFAULT_FOV_DEG * DEG2RAD;

	#endregion
	#region Methods

	public void RecalculateAllMatrices(in Matrix4x4 _mtxWorld, uint _resolutionX, uint _resolutionY)
	{
		float aspectRatio = (float)_resolutionX / _resolutionY;

		RecalculateClipSpaceMatrices(in _mtxWorld, aspectRatio);
		RecalculatePixelSpaceMatrices(_resolutionX, _resolutionY);
	}

	public void RecalculateClipSpaceMatrices(in Matrix4x4 _mtxWorld, float _aspectRatio)
	{
		if (!Matrix4x4.Invert(_mtxWorld, out mtxWorld2Camera))
		{
			mtxWorld2Camera = Matrix4x4.Identity;
		}

		RecalculateClipSpaceMatrices(_aspectRatio);
	}

	public void RecalculateClipSpaceMatrices(float _aspectRatio)
	{
		// Calculate projection matrix:
		Matrix4x4 mtxCamera2Clip;
		if (projectionType == CameraProjectionType.Perspective)
		{
			mtxCamera2Clip = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(
				fieldOfViewRad,
				_aspectRatio,
				nearClipPlane,
				farClipPlane);
		}
		else
		{
			mtxCamera2Clip = Matrix4x4.CreateOrthographicLeftHanded(
				orthographicSize * _aspectRatio,
				orthographicSize,
				nearClipPlane,
				farClipPlane);
		}

		// Assemble matrix for transforming from world space to clip space:
		mtxWorld2Clip = mtxWorld2Camera * mtxCamera2Clip;

		// Optionally mirror projection vertically:
		if (mirrorY)
		{
			mtxWorld2Clip *= Matrix4x4.CreateScale(1, -1, 1);
		}
	}

	public void RecalculatePixelSpaceMatrices(uint _resolutionX, uint _resolutionY)
	{
		mtxClip2Pixel = Matrix4x4.CreateViewportLeftHanded(0, 0, _resolutionX, _resolutionY, 0.0f, 1.0f);

		mtxWorld2Pixel = mtxWorld2Clip * mtxClip2Pixel;
		Matrix4x4.Invert(mtxWorld2Pixel, out mtxPixel2World);
	}

	public override readonly string ToString()
	{
		string sizeTxt = projectionType == CameraProjectionType.Orthographic
			? $"Size: {orthographicSize}m"
			: $"FoV: {FieldOfViewDegrees}°";
		return $"Type: {projectionType}, Clip planes: {nearClipPlane:0.##}-{farClipPlane:0.##}m, {sizeTxt}";
	}

	#endregion
}
