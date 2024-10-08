﻿using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Lighting.Data;
using FragEngine3.Graphics.Utility;
using System.Numerics;

namespace FragEngine3.Graphics.Lighting.Instances;

internal sealed class PointLight : LightInstance
{
	#region Constructors

	public PointLight(GraphicsCore _core) : base(_core)
	{
		data.type = (uint)LightType.Point;
		data.position = worldPose.position;
		data.direction = worldPose.Forward;
		data.shadowCascades = 0;
		data.shadowCascadeRange = MaxLightRange;
	}

	#endregion
	#region Fields

	private float maxLightRangeSq = 10.0f;

	#endregion
	#region Properties

	public override LightType Type => LightType.Point;

	public override float LightIntensity
	{
		get => data.intensity;
		set
		{
			data.intensity = Math.Max(value, 0.0f);
			MaxLightRange = MathF.Sqrt(data.intensity / LightConstants.MIN_LIGHT_INTENSITY);
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
		if (IsStaticLight && staticLightDirtyFlags.HasFlag(StaticLightDirtyFlags.Data)) return data;

		data.position = worldPose.position;
		data.shadowMapIdx = ShadowMapIdx;

		staticLightDirtyFlags &= ~StaticLightDirtyFlags.Data;
		return data;
	}

	public override bool CheckVisibilityByCamera(in CameraComponent _camera)
	{
		return true;
	}

	public override bool CheckIsRendererInRange(in IPhysicalRenderer _renderer)
	{
		float maxEffectRange = MaxLightRange + _renderer.BoundingRadius;
		float lightDistSq = Vector3.DistanceSquared(_renderer.VisualCenterPoint, worldPose.position);
		return lightDistSq < maxEffectRange * maxEffectRange;
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
		if (shadowCameraInstance is null || shadowCameraInstance.IsDisposed)
		{
			if (!ShadowMapUtility.UpdateOrCreateShadowMapCameraInstance(
				GraphicsCore,
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

		data.color = (Vector3)Color32.ParseHexString(_lightData.LightColor);
		LightIntensity = _lightData.LightIntensity;

		CastShadows = _lightData.CastShadows;
		ShadowCascades = _lightData.ShadowCascades;
		ShadowNormalBias = _lightData.ShadowBias;

		return true;
	}

	public override bool SaveToData(out LightData _outLightData)
	{
		_outLightData = new()
		{
			Type = LightType.Point,

			LightColor = new Color32(data.color).ToHexString(),
			LightIntensity = LightIntensity,
			SpotAngleDegrees = 180,

			CastShadows = CastShadows,
			ShadowCascades = ShadowCascades,
			ShadowBias = ShadowNormalBias,
		};
		return true;
	}

	#endregion
}
