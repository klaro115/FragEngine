using FragEngine3.Graphics.Resources.Data;
using System.Collections.Concurrent;
using Veldrid;

namespace FragEngine3.Graphics.Resources;

/// <summary>
/// Central registry and management instance for all samplers created by and used with a graphics core. This allows the easy re-use of identical samplers across all materials.
/// </summary>
/// <param name="_core">The graphics core whose samplers shall be managed.</param>
public sealed class SamplerManager(GraphicsCore _core) : IDisposable
{
	#region Constructors

	~SamplerManager()
	{
		Dispose(false);
	}

	#endregion
	#region Fields

	public readonly GraphicsCore core = _core ?? throw new ArgumentNullException(nameof(_core), "Graphics core may not be null!");

	private readonly ConcurrentDictionary<ulong, Sampler> samplerDict = new(-1, 10);

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

		Clear();
	}

	public void Clear()
	{
		foreach (var kvp in samplerDict)
		{
			kvp.Value?.Dispose();
		}
		samplerDict.Clear();
	}

	public bool GetSampler(string? _samplerDescription, out Sampler _outSampler)
	{
		if (IsDisposed)
		{
			core.graphicsSystem.engine.Logger.LogError("Cannot get sampler from disposed sampler manager!");
			_outSampler = null!;
			return false;
		}

		// Generate the sampler's ID from its description text:
		SamplerDescription desc = MaterialDataDescriptionParser.DecodeDescription_Sampler(_samplerDescription);
		ulong samplerId = MaterialDataDescriptionParser.CreateIdentifier_Sampler(desc);

		// Check if an identical sampler already exists. If so, re-use that:
		if (samplerDict.TryGetValue(samplerId, out Sampler? sampler))
		{
			if (!sampler.IsDisposed)
			{
				_outSampler = sampler;
				return true;
			}
			samplerDict.TryRemove(samplerId, out _);
		}

		// Create and register a new sampler:
		return TryCreateNewSampler(ref desc, out _outSampler);
	}

	public bool GetSampler(ulong _samplerId, out Sampler _outSampler)
	{
		if (IsDisposed)
		{
			core.graphicsSystem.engine.Logger.LogError("Cannot get sampler from disposed sampler manager!");
			_outSampler = null!;
			return false;
		}

		// Check if an identical sampler already exists. If so, re-use that:
		if (samplerDict.TryGetValue(_samplerId, out Sampler? sampler))
		{
			if (!sampler.IsDisposed)
			{
				_outSampler = sampler;
				return true;
			}
			samplerDict.TryRemove(_samplerId, out _);
		}

		// Generate a description from the information encoded in the ID:
		SamplerDescription desc = MaterialDataDescriptionParser.DecodeIdentifier_Sampler(_samplerId);

		// Create and register a new sampler:
		return TryCreateNewSampler(ref desc, out _outSampler);
	}

	public bool GetSampler(ref SamplerDescription _desc, out Sampler _outSampler)
	{
		if (IsDisposed)
		{
			core.graphicsSystem.engine.Logger.LogError("Cannot get sampler from disposed sampler manager!");
			_outSampler = null!;
			return false;
		}

		// Generate a descriptive ID for the given description:
		ulong samplerId = MaterialDataDescriptionParser.CreateIdentifier_Sampler(_desc);

		// Check if an identical sampler already exists. If so, re-use that:
		if (samplerDict.TryGetValue(samplerId, out Sampler? sampler))
		{
			if (!sampler.IsDisposed)
			{
				_outSampler = sampler;
				return true;
			}
			samplerDict.TryRemove(samplerId, out _);
		}

		// Create and register a new sampler:
		return TryCreateNewSampler(ref _desc, out _outSampler);
	}

	private bool TryCreateNewSampler(ref SamplerDescription _desc, out Sampler _outSampler)
	{
		try
		{
			_outSampler = core.MainFactory.CreateSampler(ref _desc);
			return true;
		}
		catch (Exception ex)
		{
			string descTxt = MaterialDataDescriptionParser.CreateDescription_Sampler(_desc);
			core.graphicsSystem.engine.Logger.LogException($"Failed to create texture sampler matching description '{descTxt}'!", ex);
			_outSampler = null!;
			return false;
		}
	}

	#endregion
}
