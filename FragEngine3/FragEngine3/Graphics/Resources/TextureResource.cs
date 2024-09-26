using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources;

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

	public TextureResource(string _resourceKey, Engine _engine, Texture _texture, out ResourceHandle _outHandle) : base(_resourceKey, _engine)
	{
		core = _engine.GraphicsSystem.graphicsCore;
		Texture = _texture ?? throw new ArgumentNullException(nameof(_texture), "Texture may not be null!");

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
	public override bool IsLoaded => !IsDisposed && Texture is not null && !Texture.IsDisposed;

	public Texture? Texture { get; private set; } = null;

	private Logger Logger => core.graphicsSystem.engine.Logger ?? Logger.Instance!;

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

		Texture?.Dispose();
		Texture = null;
	}

	public bool SetPixelData(RawImageData _rawImage)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot set pixel data of disposed texture resource!");
			return false;
		}
		if (!IsLoaded)
		{
			Logger.LogError("Cannot set pixel data of uninitialized texture resource!");
			return false;
		}
		if (_rawImage is null || !_rawImage.IsValid())
		{
			Logger.LogError("Cannot set pixel data of texture from null or invalid raw image data!");
			return false;
		}

		// Upload typed raw pixel data to GPU texture memory:
		return _rawImage.bitsPerPixel switch
		{
			8 => UpdateTexture(_rawImage.pixelData_MonoByte),
			32 => _rawImage.channelCount != 1
				? UpdateTexture(_rawImage.pixelData_RgbaByte)
				: UpdateTexture(_rawImage.pixelData_MonoFloat),
			128 => UpdateTexture(_rawImage.pixelData_RgbaFloat),
			_ => false,
		};


		// Local helper method for uploading pixels of arbitrary types and layouts:
		bool UpdateTexture<T>(T[]? _pixelData) where T : unmanaged
		{
			if (_pixelData is null) return false;
			try
			{
				core.Device.UpdateTexture(Texture, _pixelData, 0, 0, 0, _rawImage.width, _rawImage.height, 1, 0, 0);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to set pixel data on texture resource '{resourceKey}'!", ex);
				return false;
			}
		}
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

	public static bool CreateTexture(ResourceHandle _handle, GraphicsCore _graphicsCore, RawImageData _rawImageData, out TextureResource? _outTexture)
	{
		if (_graphicsCore is null)
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
		if (_handle is null || !_handle.IsValid)
		{
			logger.LogError("Cannot create texture using null or invalid resource handle!");
			_outTexture = null;
			return false;
		}

		// Don't reload or reassign existing/loaded textures: (old versions must be unloaded first!)
		if (_handle.IsLoaded)
		{
			Resource? oldResource = _handle.GetResource(false, false);
			if (oldResource is not null && !oldResource.IsDisposed)
			{
				logger.LogError("Cannot create texture; resource handle is already loaded and has a resource assigned!");
				_outTexture = null;
				return false;
			}
		}

		// Try creating the empty texture GPU object:
		Texture texture;
		try
		{
			TextureDescription textureDesc = _rawImageData.CreateTextureDescription();

			texture = _graphicsCore.MainFactory.CreateTexture(ref textureDesc);
			texture.Name = $"Tex_{_handle.resourceKey}_{_rawImageData.width}x{_rawImageData.height}p_{textureDesc.Format}";
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to create texture for handle '{_handle.resourceKey}'!", ex);
			_outTexture = null;
			return false;
		}

		// Create a texture resource object:
		_outTexture = new TextureResource(_graphicsCore, _handle)
		{
			Texture = texture,
		};

		// Set initial texture content from the given raw image data:
		return _outTexture.SetPixelData(_rawImageData);
	}

	#endregion
}
