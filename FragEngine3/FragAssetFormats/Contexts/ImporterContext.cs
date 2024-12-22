using System.Text.Json;

namespace FragAssetFormats.Contexts;

public sealed class ImporterContext
{
	#region Properties

	public required ILogger Logger { get; init; }

	public required JsonSerializerOptions? JsonOptions { get; init; }

	#endregion
}
