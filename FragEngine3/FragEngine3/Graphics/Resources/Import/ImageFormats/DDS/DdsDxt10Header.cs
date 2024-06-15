using FragEngine3.EngineCore;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace FragEngine3.Graphics.Resources.Import.ImageFormats.DDS;

public sealed class DdsDxt10Header
{
	#region Fields

	public Format dxgiFormat;
	public ResourceDimension resourceDimension;
	public ResourceOptionFlags miscFlag;
	public uint arraySize;
	public DdsAlphaMode alphaMode;

	#endregion
	#region Properties

	/// <summary>
	/// Whether the resource is a Texture2D cubemap.
	/// </summary>
	public bool IsTextureCube => resourceDimension == ResourceDimension.Texture2D && miscFlag.HasFlag(ResourceOptionFlags.TextureCube);

	/// <summary>
	/// Whether the alpha channel is used as alpha/transparency. If false, it used as an additional color or data value.
	/// </summary>
	public bool IsAlphaTransparency => alphaMode != DdsAlphaMode.Custom;

	#endregion
	#region Methods

	public bool IsValid()
	{
		bool result =
			dxgiFormat != Format.Unknown &&
			resourceDimension != ResourceDimension.Unknown &&
			arraySize >= 1;
		return result;
	}

	public static bool Read(BinaryReader _reader, out DdsDxt10Header _outDxt10Header)
	{
		if (_reader is null)
		{
			Logger.Instance?.LogError("Cannot read DDS DXT10 header using null binary reader!");
			_outDxt10Header = null!;
			return false;
		}	

		try
		{
			// Read data:
			_outDxt10Header = new()
			{
				dxgiFormat = (Format)_reader.ReadUInt32(),
				resourceDimension = (ResourceDimension)_reader.ReadUInt32(),
				miscFlag = (ResourceOptionFlags)_reader.ReadUInt32(),
				arraySize = _reader.ReadUInt32(),
				alphaMode = (DdsAlphaMode)_reader.ReadUInt32(),
			};

			// Check validity and return success:
			return _outDxt10Header.IsValid();
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read DDS DXT10 header!", ex);
			_outDxt10Header = null!;
			return false;
		}
	}

	public bool Write(BinaryWriter _writer)
	{
		if (!IsValid())
		{
			return false;
		}

		try
		{
			// Write data:
			_writer.Write((uint)dxgiFormat);
			_writer.Write((uint)resourceDimension);
			_writer.Write((uint)miscFlag);
			_writer.Write(arraySize);
			_writer.Write((uint)alphaMode);

			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read DDS DXT10 header!", ex);
			return false;
		}
	}

	#endregion
}
