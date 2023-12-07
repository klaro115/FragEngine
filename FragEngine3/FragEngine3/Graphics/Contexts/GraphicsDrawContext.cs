using Veldrid;

namespace FragEngine3.Graphics.Contexts
{
    public sealed class GraphicsDrawContext(
        GraphicsCore _core,
        CommandList _cmdList)
    {
        #region Fields

        public readonly GraphicsCore core = _core;
        public readonly CommandList cmdList = _cmdList;

        #endregion
    }
}
