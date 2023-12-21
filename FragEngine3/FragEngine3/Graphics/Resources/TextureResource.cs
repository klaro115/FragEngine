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

		public static bool CreateTexture(ResourceHandle _handle, GraphicsCore _graphicsCore, out TextureResource? _outTexture)
		{
			if (_graphicsCore == null)
			{
				Logger.Instance?.LogError("Cannot create texture using null graphics core!");
				_outTexture = null;
				return false;
			}
			Logger logger = _graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance!;
			if (!_graphicsCore.IsInitialized)
			{
				logger.LogError("Cannot create texture using uninitialized graphics core!");
				_outTexture = null;
				return false;
			}
			if (_handle == null || !_handle.IsValid)
			{
				logger.LogError("Cannot create texture using null or invalid resource handle!");
				_outTexture = null;
				return false;
			}

			// Don't reload or reassign existing/loaded textures: (old versions must be unloaded first!)
			if (_handle.IsLoaded)
			{
				Resource? oldResource = _handle.GetResource(false, false);
				if (oldResource != null && !oldResource.IsDisposed)
				{
					logger.LogError("Cannot create texture; resource handle is already loaded and has a resource assigned!");
					_outTexture = null;
					return false;
				}
			}

			//TODO [later]: Add import functionality for various image file formats. Consider moving this method to its own importer class.

			_outTexture = null;	//TEMP
			return false;
		}

		#endregion
	}
}
