using System.IO;

namespace Ceiro.Scripts.Core.Systems;

/// <summary>
/// Manages delta compression for save files.
/// </summary>
public static class DeltaCompression
{
	/// <summary>
	/// Compresses a chunk of data using delta compression.
	/// </summary>
	/// <param name="data">The data to compress.</param>
	/// <returns>The compressed data.</returns>
	public static byte[] CompressChunk(byte[] data)
	{
		if (data.Length == 0)
			return [];

		using var memoryStream = new MemoryStream();
		using var writer       = new BinaryWriter(memoryStream);

		// Write the first byte as is
		writer.Write(data[0]);

		// Write the deltas for the rest
		for (var i = 1; i < data.Length; i++)
		{
			var delta = (sbyte)(data[i] - data[i - 1]);
			writer.Write(delta);
		}

		return memoryStream.ToArray();
	}

	/// <summary>
	/// Decompresses a chunk of data using delta decompression.
	/// </summary>
	/// <param name="compressedData">The compressed data.</param>
	/// <param name="originalLength">The original length of the data.</param>
	/// <returns>The decompressed data.</returns>
	public static byte[] DecompressChunk(byte[] compressedData, int originalLength)
	{
		if (compressedData.Length is 0)
			return [];

		using var memoryStream = new MemoryStream(compressedData);
		using var reader       = new BinaryReader(memoryStream);

		var result = new byte[originalLength];

		// Read the first byte as is
		result[0] = reader.ReadByte();

		// Read and apply the deltas for the rest
		for (var i = 1; i < originalLength; i++)
		{
			var delta = reader.ReadSByte();
			result[i] = (byte)(result[i - 1] + delta);
		}

		return result;
	}
}