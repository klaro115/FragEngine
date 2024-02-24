using FragEngine3.Graphics.Contexts;
using Veldrid;

namespace FragEngine3.Graphics.PostProcessing;

/// <summary>
/// Interface representing a stack of post-processing effects to be applied to a framebuffer.<para/>
/// OWNERSHIP: A stack instance is generally owned by a graphics stack or camera that it is assigned to and used by.
/// If the owner/user expires, the post-processing stack should also expire and be disposed. An exception to this is
/// when the stack is unassigned from a user before disposal, whereupon ownership is transferred to the code that
/// was responsible for the unassigning. It should be reassigned to a new owner and repurposed only after unassigning
/// it.<para/>
/// RESOURCES: The stack owns all resources and graphics objects that were created by its members and that are not
/// meant to be accessed by the global resource management system. All such resources and assets should be disposed
/// and unloaded once the stack expires.
/// </summary>
public interface IPostProcessingStack : IDisposable
{
	#region Properties

	/// <summary>
	/// Gets whether the post-processing stack has been disposed. Disposed instances should no longer be accessed
	/// and references to them should be dropped.
	/// </summary>
	bool IsDisposed { get; }

	#endregion
	#region Methods

	bool Refresh();

	bool Draw(
		in SceneContext _sceneCtx,
		in CameraPassContext _cameraCtx,
		in Framebuffer _inputFramebuffer,
		out Framebuffer _outTargetFramebuffer);

	#endregion
}
