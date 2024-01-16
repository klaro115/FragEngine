using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Contexts
{
	public sealed class SceneContext(
		Scene _scene,
		DeviceBuffer _cbScene,
		ResourceLayout _resLayoutCamera,
		Sampler _samplerShadowMaps)
	{
		#region Fields

		public readonly Scene scene = _scene ?? throw new ArgumentNullException(nameof(scene), "Scene may not be null!");
		public readonly DeviceBuffer cbScene = _cbScene ?? throw new ArgumentNullException(nameof(_cbScene), "Scene-wide constant buffer may not be null!");
		public readonly ResourceLayout resLayoutCamera = _resLayoutCamera ?? throw new ArgumentNullException(nameof(_resLayoutCamera), "Resource layout for per-camera resources may not be null!");
		public readonly Sampler samplerShadowMaps = _samplerShadowMaps;

		#endregion
		#region Properties

		public bool IsValid =>
			scene != null && !scene.IsDisposed &&
			cbScene != null && !cbScene.IsDisposed &&
			resLayoutCamera != null && !resLayoutCamera.IsDisposed &&
			samplerShadowMaps != null && !samplerShadowMaps.IsDisposed;

		#endregion
	}
}
