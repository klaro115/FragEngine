using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Utility;
using System.Numerics;

namespace FragEngine3.Graphics.Lighting;

internal sealed class SpotLightInstance(GraphicsCore _core) : LightInstance(_core)
{
	#region Fields

	private float maxLightRangeSq = 10.0f;
	private float spotAngleRad = 30.0f * LightConstants.DEG2RAD;
	private float spotAngleMinDot = MathF.Cos(15.0f * LightConstants.DEG2RAD);

	#endregion
	#region Properties

	public override LightType Type => LightType.Spot;

	public override float LightIntensity
	{
		get => lightIntensity;
		set
		{
			lightIntensity = Math.Max(value, 0.0f);
			MaxLightRange = MathF.Sqrt(lightIntensity / LightConstants.MIN_LIGHT_INTENSITY);
			maxLightRangeSq = MaxLightRange * MaxLightRange;
		}
	}

	public float SpotAngleRadians
	{
		get => spotAngleRad;
		set
		{
			spotAngleRad = Math.Clamp(value, 0.0f, MathF.PI);
			spotAngleMinDot = MathF.Cos(spotAngleRad);
		}
	}
	public float SpotAngleDegrees
	{
		get => spotAngleRad * LightConstants.RAD2DEG;
		set => SpotAngleRadians = value * LightConstants.DEG2RAD;
	}

	#endregion
	#region Methods

	public override LightSourceData GetLightSourceData()
	{
		return new()
		{
			color = new Vector3(lightColor.R, lightColor.G, lightColor.B),
			intensity = LightIntensity,
			position = worldPose.position,
			type = (uint)LightType.Spot,
			direction = worldPose.Forward,
			spotMinDot = spotAngleMinDot,
			shadowMapIdx = ShadowMapIdx,
			shadowBias = ShadowBias,
			shadowCascades = ShadowCascades,
			shadowCascadeRange = ShadowMapUtility.directionalLightSize,
		};
	}

	public override bool CheckVisibilityByCamera(in Camera _camera)
	{
		return true;
	}

	protected override Matrix4x4 RecalculateShadowProjectionMatrix(Vector3 _shadingFocalPoint, uint _cascadeIdx)
	{
		// Transform from a world space position, to the light's local space, to perspective projection clip space:
		if (!Matrix4x4.Invert(worldPose.Matrix, out Matrix4x4 mtxWorld2Local))
		{
			mtxWorld2Local = Matrix4x4.Identity;
		}
		Matrix4x4 mtxLocal2Clip = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(spotAngleRad, 1, 0.01f, MaxLightRange);
		Matrix4x4 mtxWorld2Clip = mtxWorld2Local * mtxLocal2Clip;

		return mtxWorld2Clip * Matrix4x4.CreateScale(1, -1, 1);
	}

	protected override bool UpdateShadowMapCameraInstance(float _shadingFocalPointRadius)
	{
		// Ensure a camera instance is ready for drawing the scene:
		if (shadowCameraInstance == null || shadowCameraInstance.IsDisposed)
		{
			if (!ShadowMapUtility.UpdateOrCreateShadowMapCameraInstance(
				in core,
				false,
				MaxLightRange,
				SpotAngleRadians,
				ref shadowCameraInstance))
			{
				return false;
			}
		}
		return true;
	}

	public override bool LoadFromData(in LightData _lightData)
	{
		if (_lightData is null) return false;

		lightColor = _lightData.LightColor;
		LightIntensity = _lightData.LightIntensity;
		SpotAngleDegrees = _lightData.SpotAngleDegrees;
		
		CastShadows = _lightData.CastShadows;
		ShadowCascades = _lightData.ShadowCascades;
		ShadowBias = _lightData.ShadowBias;

		return true;
	}

	public override bool SaveToData(out LightData _outLightData)
	{
		_outLightData = new()
		{
			Type = LightType.Spot,

			LightColor = lightColor,
			LightIntensity = LightIntensity,
			SpotAngleDegrees = SpotAngleDegrees,
			
			CastShadows = CastShadows,
			ShadowCascades = ShadowCascades,
			ShadowBias = ShadowBias,
		};
		return true;
	}

	#endregion
}
