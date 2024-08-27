<h1>Asset & Resource Guide</h1>

Resources are primarily loaded from files located within the "data" folder. Each resource file might contain one or more resources, and must be accompanied by a descriptive resource metadata file.

- [Metadata Files](#metadata-files)
- [Data Files](#data-files)
    - [Single-Resource Files](#single-resource-files)
    - [Packaged Files](#packaged-files)

<br>

## Metadata Files
_Source code:_ [ResourceFileData](../../FragEngine3/Resources/Data/ResourceFileData.cs), [ResourceHandleData](../../FragEngine3/Resources/Data/ResourceHandleData.cs)

These files must have the ".fres" file extension, which is short for <ins>F</ins>ragment <ins>Res</ins>ource. The file's contents are a JSON-representation of the `ResourceFileData` type. The `DataFilePath` field points at another file containing the resource's data, and the `Resources` field provides a list of descriptions of all resources contained within the file. Resource descriptions are JSON-representations of the `ResourceHandleData` type.

See the [Resource Specifications](./Resource%20Specifications.md) for more in-depth information on resource files.
<br>


## Data Files

Alongside each metadata file exists a data file that contains the actual asset data. This can be images, shaders, audio, or even serialized data formats such as CSV or JSON. In general, there are two types of data files: single-resource files and packaged resource files.

#### Single-Resource Files

These files only ever contain one resource. This type is useful for development or for large unique assets. Single-resource files should always have the standard file extension of the data they contain, such as .FBX for 3D models, or .PNG for image files. If the metadata file is incomplete or contains conflicting information, some of that can be reconstructed at run-time using the file name and extension.

#### Packaged Files

These are compressed file archives containing a batch of one or more resources whose combined data has been compressed to reduce storage size. The metadata file must be valid for packaged files, as it contains the exact data locations for each asset within the data file. Packaged files may be compressed from either conntiguous memory, or using block compression.

Packaged resource files should generally have the file extensions ".cpkg" and ".bpkg". The former identifies contiguously compressed data, whilst the latter identifies block-compressed data.
Contiguously compressed data requires that all data within the file must be decompressed and kept in memory once the first resource in the package is loaded, which may be useful if all assets belong together and will be loaded en-bloc anyways. No additional file access or decompression will be required after the first asset is accessed.
Block-compression allow for multiple independent assets to sit in a same package, each's data starting with a new compression block, thus allowing reads in random order and without needing to decompress or read any adjacent assets' data.

In future iterations, the ability to use encrypted packaged files may be added, to prevent third-parties from stealing assets from your released app.
