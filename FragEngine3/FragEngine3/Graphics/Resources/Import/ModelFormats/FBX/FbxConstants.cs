namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

internal static class FbxConstants
{
	#region Constants

	public const string NODE_NAME_OBJECTS = "Objects";						// Objects
	public const string NODE_NAME_GEOMETRY = "Geometry";					// Objects -> Geometry

	// Positions & Indices:
	public const string NODE_NAME_VERTICES = "Vertices";					// Objects -> Geometry -> Vertices
	public const string NODE_NAME_INDICES = "PolygonVertexIndex";			// Objects -> Geometry -> PolygonVertexIndex

	// Normals:
	public const string NODE_NAME_LAYER_ELEM_NORMAL = "LayerElementNormal";	// Objects -> Geometry -> LayerElementNormal
	public const string NODE_NAME_LAYER_NORMALS = "Normals";                // Objects -> Geometry -> LayerElementNormal -> Normals

	// UVs:
	public const string NODE_NAME_LAYER_ELEM_UVs = "LayerElementUV";        // Objects -> Geometry -> LayerElementUV
	public const string NODE_NAME_LAYER_UVs = "UV";                         // Objects -> Geometry -> LayerElementUV -> UV
	public const string NODE_NAME_LAYER_UV_INDEX = "UVIndex";               // Objects -> Geometry -> LayerElementUV -> UVIndex

	#endregion
}
