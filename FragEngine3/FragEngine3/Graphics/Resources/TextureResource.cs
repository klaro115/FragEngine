using FragEngine3.EngineCore;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public sealed class TextureResource : Resource
	{
		#region Constructors

		public TextureResource(GraphicsCore _graphicsCore, ResourceHandle _handle) : base(_handle)
		{
			core = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Graphics core may not be null!");
		}

		public TextureResource(string _resourceKey, Engine _engine, out ResourceHandle _outHandle) : base(_resourceKey, _engine)
		{
			core = _engine.GraphicsSystem.graphicsCore;

			_outHandle = new(this);
			resourceManager.AddResource(_outHandle);
		}

		~TextureResource()
		{
			Dispose(false);
		}

		#endregion
		#region Fields

		public readonly GraphicsCore core;

		#endregion
		#region Properties

		public override ResourceType ResourceType => ResourceType.Texture;
		public override bool IsLoaded => !IsDisposed && Texture != null && !Texture.IsDisposed;

		public Texture? Texture { get; private set; } = null;

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			base.Dispose(_disposing);

			Texture?.Dispose();
			Texture = null;
		}

		//...

		public override IEnumerator<ResourceHandle> GetResourceDependencies()
		{
			if (resourceManager.GetResource(resourceKey, out ResourceHandle handle))
			{
				yield return handle;
			}
		}

		public override string ToString()
		{
			if (!IsLoaded) return "Texture (not loaded)";

			string sizeTxt = Texture!.Type switch
			{
				TextureType.Texture1D => Texture.Width.ToString(),
				TextureType.Texture2D => $"{Texture.Width}x{Texture.Height}",
				_ => $"{Texture.Width}x{Texture.Height}x{Texture.Depth}",
			};
			return $"Texture '{resourceKey}' (Size: {sizeTxt}, Format: {Texture.Format})";
		}

		#endregion
	}
}
