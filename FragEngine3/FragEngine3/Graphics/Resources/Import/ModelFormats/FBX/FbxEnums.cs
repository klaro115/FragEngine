using System.Numerics;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

internal enum FbxPropertyType
{
	RawBytes,			// R
	String,				// S

	PropertyArray,		// other

	Int16,				// Y
	Boolean,			// C, b
	Int32,				// I, i
	Float,				// F, f
	Double,				// D, d
	Int64,				// L, l
}

internal enum FbxMappingType
{
	ByPolygonVertices,	// Different values for each vertex.
	AllSame,			// All vertices use one same value.
	//...
}

internal enum FbxReferenceInfoType
{
	Direct,				// Values are listed in-order for each vertex.
	IndexToDirect,		// Indices for each vertex map to an array of values.
	//...
}
