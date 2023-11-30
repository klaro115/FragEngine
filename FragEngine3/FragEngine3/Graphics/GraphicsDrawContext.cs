using Veldrid;

namespace FragEngine3.Graphics
{
	public sealed class GraphicsDrawContext(GraphicsCore _core, CommandList _cmdList, OutputDescription _outputDesc)
	{
		#region Fields

		public readonly GraphicsCore core = _core;
		public readonly CommandList cmdList = _cmdList;
		public readonly OutputDescription outputDesc = _outputDesc;
		
		public bool outputDescChanged = false;

		#endregion
	}
}
