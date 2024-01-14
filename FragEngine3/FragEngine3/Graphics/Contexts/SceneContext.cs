using FragEngine3.Scenes;
using Veldrid;

namespace FragEngine3.Graphics.Contexts
{
	public sealed class SceneContext(
		Scene _scene,
		DeviceBuffer _cbScene)
	{
		#region Fields

		public readonly Scene scene = _scene ?? throw new ArgumentNullException(nameof(scene), "Scene may not be null!");
		public readonly DeviceBuffer cbScene = _cbScene ?? throw new ArgumentNullException(nameof(_cbScene), "Scene-wide constant buffer may not be null!");

		#endregion
		#region Properties

		public bool IsValid =>
			scene != null && !scene.IsDisposed &&
			cbScene != null && !cbScene.IsDisposed;

		#endregion
	}
}
