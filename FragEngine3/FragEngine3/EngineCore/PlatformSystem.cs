
namespace FragEngine3.EngineCore
{
	public sealed class PlatformSystem : IDisposable
	{
		#region Constructors

		public PlatformSystem(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
		}

		~PlatformSystem()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Engine engine;

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
			//...
		}

		#endregion
	}
}
