using Veldrid;

namespace FragEngine3.Graphics.Components.Internal
{
	internal sealed class CameraPassResources : IDisposable
	{
		#region Constructors

		~CameraPassResources()
		{
			Dispose(false);
		}

		#endregion
		#region Fields

		public DeviceBuffer? cbCamera;
		public ResourceSet? defaultCameraResourceSet;

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _disposing)
		{
			IsDisposed = true;

			cbCamera?.Dispose();
			defaultCameraResourceSet?.Dispose();

			if (_disposing )
			{
				cbCamera = null;
				defaultCameraResourceSet = null;
			}
		}
		
		#endregion
	}
}
