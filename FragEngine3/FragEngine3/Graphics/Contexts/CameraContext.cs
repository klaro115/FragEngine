﻿using FragEngine3.Graphics.Components;
using Veldrid;

namespace FragEngine3.Graphics.Contexts
{
	public sealed class CameraContext(Camera _camera, DeviceBuffer _globalConstantBuffer, DeviceBuffer _lightDataBuffer, OutputDescription _outputDesc)
	{
		#region Fields

		public readonly Camera camera = _camera;
		public readonly DeviceBuffer globalConstantBuffer = _globalConstantBuffer;
		public readonly DeviceBuffer lightDataBuffer = _lightDataBuffer;
		public readonly OutputDescription outputDesc = _outputDesc;

		#endregion
	}
}