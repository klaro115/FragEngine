using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Utility;
using FragEngine3.Scenes;
using System.Numerics;
using FragEngine3.Graphics.Lighting.Data;

namespace FragEngine3.Graphics.Lighting.Instances;

internal sealed class DirectionalLightInstance : LightInstance
{
    #region Constructors

    public DirectionalLightInstance(GraphicsCore _core) : base(_core)
    {
        data.type = (uint)LightType.Directional;
        data.direction = worldPose.Forward;
        data.shadowCascadeRange = LightConstants.directionalLightSize;
    }

    #endregion
    #region Fields

    private static readonly bool rotateProjectionAlongCamera = false;

    #endregion
    #region Properties

    public override LightType Type => LightType.Directional;

    public override float LightIntensity
    {
        get => data.intensity;
        set
        {
            data.intensity = Math.Max(value, 0.0f);
            MaxLightRange = 1.0e+8f;
        }
    }

    #endregion
    #region Methods

    public override LightSourceData GetLightSourceData()
    {
        data.direction = worldPose.Forward;
        data.shadowMapIdx = ShadowMapIdx;
        data.shadowCascades = ShadowCascades;
        data.shadowCascadeRange = LightConstants.directionalLightSize;

        return data;
    }

    public override bool CheckVisibilityByCamera(in Camera _camera)
    {
        return true;
    }

    protected override Matrix4x4 RecalculateShadowProjectionMatrix(Vector3 _shadingFocalPoint, uint _cascadeIdx)
    {
        float maxRange = LightConstants.directionalLightSize * Math.Max(MathF.Pow(2, ShadowCascades), 1);
        float maxDirectionalRange = LightConstants.directionalLightSize * MathF.Floor(MathF.Pow(2, _cascadeIdx));

        Vector3 lightDir = worldPose.Forward;
        Quaternion worldRot = worldPose.rotation;

        // Orient light map projection to have its pixel grid roughly aligned with the camera's direction:
        if (rotateProjectionAlongCamera && Camera.MainCamera != null)
        {
            Vector3 worldUp = Vector3.Transform(Vector3.UnitY, worldRot);
            Vector3 cameraDir = Camera.MainCamera.node.WorldForward;

            Vector3 lightDirProj = Vector3.Normalize(cameraDir.ProjectToPlane(lightDir));
            float cameraRotAngle = VectorExt.Angle(worldUp, lightDirProj, true);
            Quaternion cameraRot = Quaternion.CreateFromAxisAngle(lightDir, cameraRotAngle);
            worldRot = cameraRot * worldRot;
        }

        // Transform from a world space position (relative to a given focal point), to orthographics projection space, to shadow map UV coordinates:
        Vector3 posOrigin = _shadingFocalPoint - lightDir * maxRange * 0.5f;
        Pose originPose = new(posOrigin, worldRot, Vector3.One, false);
        if (!Matrix4x4.Invert(originPose.Matrix, out Matrix4x4 mtxWorld2Local))
        {
            mtxWorld2Local = Matrix4x4.Identity;
        }
        Matrix4x4 mtxLocal2Clip = Matrix4x4.CreateOrthographicLeftHanded(maxDirectionalRange, maxDirectionalRange, 0.01f, maxRange);
        Matrix4x4 mtxWorld2Clip = mtxWorld2Local * mtxLocal2Clip;

        mtxWorld2Clip *= Matrix4x4.CreateScale(1, -1, 1);
        return mtxWorld2Clip;
    }

    protected override bool UpdateShadowMapCameraInstance(float _shadingFocalPointRadius)
    {
        // Ensure a camera instance is ready for drawing the scene:
        if (shadowCameraInstance == null || shadowCameraInstance.IsDisposed)
        {
            if (!ShadowMapUtility.UpdateOrCreateShadowMapCameraInstance(
                GraphicsCore,
                true,
                MaxLightRange,
                0.0f,
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
        ShadowBias = _lightData.ShadowBias;

        return true;
    }

    public override bool SaveToData(out LightData _outLightData)
    {
        _outLightData = new()
        {
            Type = LightType.Directional,

            LightColor = new Color32(data.color).ToHexString(),
            LightIntensity = data.intensity,
            SpotAngleDegrees = 0,

            CastShadows = CastShadows,
            ShadowCascades = ShadowCascades,
            ShadowBias = ShadowBias,
        };
        return true;
    }

    #endregion
}
