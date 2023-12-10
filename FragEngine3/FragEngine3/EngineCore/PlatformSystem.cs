
namespace FragEngine3.EngineCore
{
	public sealed class PlatformSystem(Engine _engine) : IEngineSystem
	{
		#region Constructors

		~PlatformSystem()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly Engine engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;

		public Engine Engine => engine;

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

		#endregion
	}
}
