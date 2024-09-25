using System.Runtime.InteropServices;
using FragEngine3.Resources;

namespace FragEngine3.EngineCore;

public sealed class PlatformSystem : IEngineSystem
{
	#region Types

	private sealed class FileExtMapping(OSPlatform _os, string _extension)
	{
		public readonly OSPlatform os = _os;
		public readonly string extension = _extension ?? string.Empty;
	}

	#endregion
	#region Constructors

	public PlatformSystem(Engine _engine)
	{
		engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

		// Identify platform OS:
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				osPlatform = OSPlatform.Windows;
				osPlatformFlag |= EnginePlatformFlag.OS_Windows;
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				osPlatform = OSPlatform.OSX;
				osPlatformFlag |= EnginePlatformFlag.OS_MacOS;
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				osPlatform = OSPlatform.Linux;
				osPlatformFlag |= EnginePlatformFlag.OS_Linux;
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
			{
				osPlatform = OSPlatform.FreeBSD;
				osPlatformFlag |= EnginePlatformFlag.OS_FreeBSD;
			}
			else
			{
				osPlatform = OSPlatform.Create(RuntimeInformation.RuntimeIdentifier);
				osPlatformFlag |= EnginePlatformFlag.OS_Other;
			}
		}

		PlatformFlags = osPlatformFlag;
	}

	~PlatformSystem()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly Engine engine;

	public readonly OSPlatform osPlatform;
	public readonly EnginePlatformFlag osPlatformFlag;

	private static readonly Dictionary<ResourceType, FileExtMapping[]> resourceFileExtMappings = new()
	{
		[ResourceType.Shader] =
		[
			new FileExtMapping(OSPlatform.Windows, ".hlsl"),
			new FileExtMapping(OSPlatform.OSX, ".metal"),
			new FileExtMapping(OSPlatform.Linux, ".glsl"),
			new FileExtMapping(OSPlatform.Linux, ".spv"),
		],
	};

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public Engine Engine => engine;

	public EnginePlatformFlag PlatformFlags { get; private set; } = 0;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	private void Dispose(bool _)
	{
		IsDisposed = true;
		//...
	}

	public void UpdatePlatformFlags()
	{
		EnginePlatformFlag prevPlatformFlags = PlatformFlags;

		PlatformFlags = osPlatformFlag;
		PlatformFlags |= engine.GraphicsSystem.graphicsCore.ApiPlatformFlag;
		//...

		if (PlatformFlags != prevPlatformFlags)
		{
			engine.Logger.LogStatus($"PlatformSystem: Platform flags changed => '{PlatformFlags}'");
		}
	}

	public bool AdjustForPlatformSpecificFileExtension(ResourceType _resourceType, string _filePath, out string _outAdjustedPath)
	{
		if (string.IsNullOrEmpty(_filePath))
		{
			_outAdjustedPath = string.Empty;
			return false;
		}

		if (!resourceFileExtMappings.TryGetValue(_resourceType, out FileExtMapping[]? mappings))
		{
			_outAdjustedPath = _filePath;
			return false;
		}

		string ext = Path.GetExtension(_filePath).ToLowerInvariant();
		foreach (FileExtMapping mapping in mappings)
		{
			if (mapping.os == osPlatform && string.CompareOrdinal(mapping.extension, ext) != 0)
			{
				_outAdjustedPath = Path.ChangeExtension(_filePath, mapping.extension);
				return true;
			}
		}

		_outAdjustedPath = _filePath;
		return false;
	}

	#endregion
}
