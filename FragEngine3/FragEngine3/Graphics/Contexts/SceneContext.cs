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
		Texture _texShadowMaps,
		DeviceBuffer _bufShadowMatrices,
		Sampler _samplerShadowMaps,

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
		public readonly Texture texShadowMaps = _texShadowMaps;
		public readonly DeviceBuffer bufShadowMatrices = _bufShadowMatrices;
		public readonly Sampler samplerShadowMaps = _samplerShadowMaps;

		// Parameters:
		public readonly uint lightCount = _lightCount;
		public readonly uint lightCountShadowMapped = Math.Min(_lightCountShadowMapped, _lightCount);

		#endregion
	}
}
