using Veldrid;

namespace FragEngine3.Graphics.Components.Internal
{
	internal sealed class CameraTarget : IDisposable
	{
		#region Constructors

		public CameraTarget(GraphicsCore _graphicsCore, RenderMode _renderMode, Texture _texColorTarget, Texture _texDepthTarget, Framebuffer _framebuffer, bool _hasOwnershipOfResources = true)
		{
			graphiceCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Graphics core may not be null!");
			renderMode = _renderMode;

			texColorTarget = _texColorTarget;
			texDepthTarget = _texDepthTarget;
			framebuffer = _framebuffer ?? throw new ArgumentNullException(nameof(_framebuffer), "Frame buffer may not be null!");
			outputDesc = framebuffer.OutputDescription;

			resolutionX = framebuffer.Width;
			resolutionY = framebuffer.Height;
			aspectRatio = (float)resolutionX / resolutionY;

			colorFormat = framebuffer.OutputDescription.ColorAttachments[0].Format;

			hasOwnershipOfResources = _hasOwnershipOfResources;
			hasDepth = framebuffer.OutputDescription.DepthAttachment != null;
			if (hasDepth)
			{
				depthFormat = framebuffer.OutputDescription.DepthAttachment!.Value.Format;
				hasStencil = depthFormat == PixelFormat.D24_UNorm_S8_UInt || depthFormat == PixelFormat.D32_Float_S8_UInt;
			}

			descriptorID = CreateDescriptorID(resolutionX, resolutionY, colorFormat, depthFormat, hasDepth, hasStencil);
		}

		public CameraTarget(GraphicsCore _graphicsCore, RenderMode _renderMode, uint _resolutionX, uint _resolutionY, PixelFormat? _colorFormat = null, bool _createDepth = true)
		{
			graphiceCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Graphics core may not be null!");
			renderMode = _renderMode;

			colorFormat = _colorFormat ?? _graphicsCore.DefaultColorTargetPixelFormat;

			texColorTarget = null!;
			texDepthTarget = null!;

			if (!_graphicsCore.CreateRenderTargets(
				colorFormat,
				_resolutionX,
				_resolutionY,
				_createDepth,
				out texColorTarget,
				out texDepthTarget!,
				out framebuffer))
			{
				Dispose();
				throw new Exception($"Failed to create render targets for camera target instance! (Size: {_resolutionX}x{_resolutionY}, Format: {_colorFormat}, Depth: {_createDepth})");
			}
			outputDesc = framebuffer.OutputDescription;

			resolutionX = _resolutionX;
			resolutionY = _resolutionY;
			aspectRatio = (float)resolutionX / resolutionY;

			hasOwnershipOfResources = true;
			hasDepth = _createDepth;
			if (hasDepth)
			{
				depthFormat = framebuffer.OutputDescription.DepthAttachment!.Value.Format;
				hasStencil = depthFormat == PixelFormat.D24_UNorm_S8_UInt || depthFormat == PixelFormat.D32_Float_S8_UInt;
			}

			descriptorID = CreateDescriptorID(resolutionX, resolutionY, colorFormat, depthFormat, hasDepth, hasStencil);
		}

		~CameraTarget()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		public readonly GraphicsCore graphiceCore;
		public readonly RenderMode renderMode;

		public readonly Texture texColorTarget;
		public readonly Texture texDepthTarget;
		public readonly Framebuffer framebuffer;

		public readonly uint resolutionX;
		public readonly uint resolutionY;
		public readonly float aspectRatio;

		public readonly PixelFormat colorFormat;
		public readonly PixelFormat depthFormat = PixelFormat.R8_UNorm;
		public readonly OutputDescription outputDesc;

		public readonly bool hasOwnershipOfResources;
		public readonly bool hasDepth;
		public readonly bool hasStencil = false;

		public readonly ulong descriptorID;

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
		private void Dispose(bool _)
		{
			IsDisposed = true;

			if (hasOwnershipOfResources)
			{
				framebuffer?.Dispose();
				texColorTarget?.Dispose();
				texDepthTarget?.Dispose();
			}
		}

		/// <summary>
		/// Creates a descriptive ID number allowing fast numerical comparisons of different camera targets for compatibility checking.
		/// </summary>
		/// <returns>A 64-bit unsigned integer with all descriptive metrics packed in it, similar to a checksum.</returns>
		public static ulong CreateDescriptorID(uint _resolutionX, uint _resolutionY, PixelFormat _colorFormat, PixelFormat _depthFormat, bool _hasDepth, bool _hasStencil)
		{
			ulong id = 0;

			// Resolution (max. 16K), bits 0-27
			id |= (_resolutionX) << 0;
			id |= (_resolutionY) << 14;

			// Pixel formats (max. value = 60), bits 28-39
			id |= (ulong)_colorFormat << 28;
			id |= (ulong)_depthFormat << 34;

			// Depth & stencil flags, bits 40-41
			id |= (_hasDepth ? 1ul : 0) << 40;
			id |= (_hasStencil ? 1ul : 0) << 41;

			return id;
		}

		public override string ToString()
		{
			return $"CameraTarget (Size: {resolutionX}x{resolutionY}, Color format: {colorFormat}, Depth: {(hasDepth ? depthFormat.ToString() : "False")}, Stencil: {hasStencil}, Ownership: {hasOwnershipOfResources})";
		}

		#endregion
	}
}
