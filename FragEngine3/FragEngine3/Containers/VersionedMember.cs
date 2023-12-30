
namespace FragEngine3.Containers
{
	public struct VersionedMember<T>(T _value, uint _version = 0)
	{
		#region Fields

		private T value = _value;
		private uint version = _version;

		#endregion
		#region Properties

		public readonly T Value => value;
		public readonly uint Version => version;

		#endregion
		#region Methods

		public readonly void DisposeValue()
		{
			if (value is IDisposable disp)
			{
				disp.Dispose();
			}
		}

		public readonly bool GetValue(uint _newestVersion, out T? _outValue)
		{
			if (_newestVersion == version)
			{
				_outValue = value;
				return true;
			}
			_outValue = default;
			return false;
		}

		public void UpdateValue(uint _newVersion, T _newValue)
		{
			version = _newVersion;
			value = _newValue;
		}

		public void UpdateValueAndVersion(T _newValue)
		{
			version++;
			value = _newValue;
		}

		public override readonly string ToString()
		{
			return $"{value?.ToString() ?? "NULL"} (v{version})";
		}

		#endregion
	}
}
