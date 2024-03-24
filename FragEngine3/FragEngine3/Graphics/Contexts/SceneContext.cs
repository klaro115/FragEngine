using FragEngine3.Graphics.Lighting;
using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Contexts
{
	public sealed class SceneContext(
		// References:
		Scene _scene,

		// Scene resources:
		ResourceLayout _resLayoutCamera,
		ResourceLayout _resLayoutObject,
		DeviceBuffer _cbScene,
		ShadowMapArray _shadowMapArray,
		DeviceBuffer _bufShadowMatrices,

		// Parameters:
		uint _lightCount,
		uint _lightCountShadowMapped)
	{
		#region Fields

		// References:
		public readonly Scene scene = _scene;

		// Scene resources:
		public readonly ResourceLayout resLayoutCamera = _resLayoutCamera;
		public readonly ResourceLayout resLayoutObject = _resLayoutObject;
		public readonly DeviceBuffer cbScene = _cbScene;
		public readonly ShadowMapArray shadowMapArray = _shadowMapArray;
		public readonly DeviceBuffer bufShadowMatrices = _bufShadowMatrices;

		// Parameters:
		public readonly uint lightCount = _lightCount;
		public readonly uint lightCountShadowMapped = Math.Min(_lightCountShadowMapped, _lightCount);

		#endregion
		#region Properties

		public Sampler SamplerShadowMaps => shadowMapArray.SamplerShadowMaps;

		#endregion
	}
}
