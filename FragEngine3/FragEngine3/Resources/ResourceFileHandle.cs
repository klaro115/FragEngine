using FragEngine3.Resources.Data;

namespace FragEngine3.Resources
{
	public sealed class ResourceFileHandle : IEquatable<ResourceFileHandle>
	{
		#region Constructors

		public ResourceFileHandle(ResourceFileData _data, string _resourceFilePath)
		{
			if (_data == null) throw new ArgumentNullException(nameof(_data), "Resource file handle data may not be null!");

			resourceFilePath = _resourceFilePath ?? throw new ArgumentNullException(nameof(_resourceFilePath), "Resource file path may not be null!");
			dataFilePath = _data.DataFilePath;
			dataFileType = _data.DataFileType;
			dataFileSize = _data.DataFileSize;

			uncompressedFileSize = _data.UncompressedFileSize;
			blockSize = _data.BlockSize;
			blockCount = _data.BlockCount;

			if (_data.Resources != null)
			{
				int resourceCount = Math.Min(_data.ResourceCount, _data.Resources.Length);
				string[] resourceKeys = new string[resourceCount];
				for (int i = 0; i < resourceCount; i++)
				{
					resourceKeys[i] = _data.Resources[i].ResourceKey ?? string.Empty;
				}
				resources = resourceKeys;
			}
			else
			{
				resources = none.resources;
			}
		}

		private ResourceFileHandle()
		{
			resourceFilePath = string.Empty;
			dataFilePath = string.Empty;
			dataFileType = ResourceFileType.None;
			dataFileSize = 0;

			uncompressedFileSize = 0;
			blockSize = 0;
			blockCount = 0;

			resources = [];
		}

		#endregion
		#region Fields

		// File details:
		public readonly string resourceFilePath = string.Empty;
		public readonly string dataFilePath = string.Empty;
		public readonly ResourceFileType dataFileType = ResourceFileType.Single;
		public readonly ulong dataFileSize;

		// Compression details:
		public readonly ulong uncompressedFileSize;
		public readonly ulong blockSize;
		public readonly uint blockCount;

		// Content details:
		public string[] resources;

		private static readonly ResourceFileHandle none = new();

		#endregion
		#region Properties

		public bool IsValid => !string.IsNullOrEmpty(resourceFilePath) && !string.IsNullOrEmpty(dataFilePath) && dataFileSize != 0 && ResourceCount != 0;
		public bool IsLoaded => LoadState == ResourceLoadState.Loaded;

		public int ResourceCount => resources != null ? resources.Length : 0;

		public ResourceLoadState LoadState { get; internal set; } = ResourceLoadState.NotLoaded;

		public static ResourceFileHandle None => none;

		#endregion
		#region Methods

		public bool GetResourceFileData(ResourceManager _resourceManager, out ResourceFileData? _outData)
		{
			if (_resourceManager == null || _resourceManager.IsDisposed)
			{
				_outData = null;
				return false;
			}

			ResourceHandleData[] resourceData = new ResourceHandleData[ResourceCount];
			for (int i = 0; i < ResourceCount; ++i)
			{
				if (!_resourceManager.GetResource(resources[i], out ResourceHandle handle) || !handle.GetResourceHandleData(out resourceData[i]))
				{
					_outData = null;
					return false;
				}
			}

			_outData = new()
			{
				DataFilePath = dataFilePath,
				DataFileType = dataFileType,
				DataFileSize = dataFileSize,

				UncompressedFileSize = uncompressedFileSize,
				BlockSize = blockSize,
				BlockCount = blockCount,

				ResourceCount = ResourceCount,
				Resources = resourceData,
			};
			return true;
		}

		public bool Equals(ResourceFileHandle? other) => ReferenceEquals(this, other) || string.CompareOrdinal(other?.dataFilePath, dataFilePath) == 0;
		public override bool Equals(object? obj) => obj is ResourceFileHandle other && Equals(other);
		public override int GetHashCode() => base.GetHashCode();

		public override string ToString()
		{
			return $"{dataFilePath ?? "NULL"} ({dataFileType}, {LoadState}) [Size: {uncompressedFileSize}, BS: {blockSize}, BC: {blockCount}]";
		}

		#endregion
	}
}
