using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Utility;
using System.Numerics;

namespace FragEngine3.Graphics.Lighting;

internal sealed class PointLightInstance(GraphicsCore _core) : LightInstance(_core)
{
	#region Fields

	private float maxLightRangeSq = 10.0f;

	#endregion
	#region Properties

	public override LightType Type => LightType.Point;

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

	public override bool CastShadows
	{
		get => false;
		set => castShadows = false;
	}
	
	public override uint MaxShadowCascades => 0;

	#endregion
	#region Methods

	public override LightSourceData GetLightSourceData()
	{
		return new()
		{
			color = new Vector3(lightColor.R, lightColor.G, lightColor.B),
			intensity = LightIntensity,
			position = worldPose.position,
			type = (uint)LightType.Point,
			direction = Vector3.UnitZ,
			spotMinDot = 0,
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
		// NOTE: Not supported at this time, as there is no linear way of evenly projecting a sphere surface to a square framebuffer.
		// Yes, I know that cubemaps exist, but I kind of don't feel like doing that just yet. Might repurpose cascade-like mapping for it though...
		return Matrix4x4.Identity * Matrix4x4.CreateScale(1, -1, 1);
	}

	protected override bool UpdateShadowMapCameraInstance(float _shadingFocalPointRadius)
	{
		// Ensure a camera instance is ready for drawing the scene:
		if (shadowCameraInstance == null || shadowCameraInstance.IsDisposed)
		{
			if (!ShadowMapUtility.UpdateOrCreateShadowMapCameraInstance(
				in core,
				false,
				_shadingFocalPointRadius,
				MathF.PI,
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

		CastShadows = _lightData.CastShadows;
		ShadowCascades = _lightData.ShadowCascades;
		ShadowBias = _lightData.ShadowBias;

		return true;
	}

	public override bool SaveToData(out LightData _outLightData)
	{
		_outLightData = new()
		{
			Type = LightType.Point,
			
			LightColor = lightColor,
			LightIntensity = LightIntensity,
			SpotAngleDegrees = 180,

			CastShadows = CastShadows,
			ShadowCascades = ShadowCascades,
			ShadowBias = ShadowBias,
		};
		return true;
	}

	#endregion
}
