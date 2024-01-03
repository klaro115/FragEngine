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
	public float fieldOfViewRad = DEFAULT_FOV_RAD;
	public float othographicSize = 5.0f;
	
	public Matrix4x4 mtxWorld2Camera = Matrix4x4.Identity;
	public Matrix4x4 mtxViewport = Matrix4x4.Identity;
	public Matrix4x4 mtxWorld2Clip = Matrix4x4.Identity;

	public Matrix4x4 mtxWorld2Pixel = Matrix4x4.Identity;
	public Matrix4x4 mtxPixel2World = Matrix4x4.Identity;

	#endregion
	#region Properties

	public float FieldOfViewDegrees
	{
		readonly get => fieldOfViewRad * MathF.PI / 180.0f;
		set => fieldOfViewRad = value * 180.0f / MathF.PI;
	}

	#endregion
	#region Constants

	public const float DEFAULT_FOV_DEG = 60.0f;
	public const float DEFAULT_FOV_RAD = DEFAULT_FOV_DEG * (MathF.PI / 180.0f);

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
		Matrix4x4 mtxProjection = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(
			fieldOfViewRad,
			_aspectRatio,
			nearClipPlane,
			farClipPlane);

		mtxWorld2Clip = mtxProjection * mtxWorld2Camera;
	}

	public void RecalculatePixelSpaceMatrices(uint _resolutionX, uint _resolutionY)
	{
		mtxViewport = Matrix4x4.CreateViewportLeftHanded(0, 0, _resolutionX, _resolutionY, 0.0f, 1.0f);

		mtxWorld2Pixel = mtxWorld2Clip * mtxViewport;
		Matrix4x4.Invert(mtxWorld2Pixel, out mtxPixel2World);
	}

	public override readonly string ToString()
	{
		string sizeTxt = projectionType == CameraProjectionType.Orthographic
			? $"Size: {othographicSize}m"
			: $"FoV: {FieldOfViewDegrees}°";
		return $"Type: {projectionType}, Clip planes: {nearClipPlane:0.##}-{farClipPlane:0.##}m, {sizeTxt}";
	}

	#endregion
}
