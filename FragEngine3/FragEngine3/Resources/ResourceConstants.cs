﻿using FragEngine3.Resources.Data;
using System.Collections.Frozen;

namespace FragEngine3.Resources;

public static class ResourceConstants
{
	#region Constants

	/// <summary>
	/// File extension for all resource metadata files. The extension must be lower-case. The contents must be a JSON-serialized object of type '<see cref="ResourceFileData"/>'.
	/// </summary>
	public const string FILE_EXT_METADATA = ".fres";

	public const string FILE_EXT_BATCH_NORMAL_COMPRESSED = ".cpkg";
	public const string FILE_EXT_BATCH_BLOCK_COMPRESSED = ".bpkg";

	public const string RESOURCE_ROOT_DIR_REL_PATH = "data";
	public const string RESOURCE_CORE_DIR_REL_PATH = "core";
	public const string RESOURCE_MODS_DIR_REL_PATH = "mods";

	#endregion
	#region Fields

	public static readonly FrozenSet<string> coreResourceLibraries = new string[]
	{
		"textures",
		"videos",
		"shaders",
		"materials",
		"animations",
		"models",
		"audio",
		"prefabs",
		"data",
	}.ToFrozenSet();

	#endregion
}
