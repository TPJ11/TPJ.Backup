using System.IO.Compression;

namespace TPJ.Backup.Shared;

public static class GZip
{
    public static async Task<byte[]> CompressAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var buffer = await File.ReadAllBytesAsync(filePath, cancellationToken);

        using var memoryStream = new MemoryStream();
        using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
        {
            gZipStream.Write(buffer, 0, buffer.Length);
        }

        return memoryStream.ToArray();
    }

    public static async Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(compressedData);
        return await DecompressAsync(stream, cancellationToken);
    }

    public static async Task<byte[]> DecompressAsync(MemoryStream compressedData, CancellationToken cancellationToken = default)
    {
        using var gZipStream = new GZipStream(compressedData, CompressionMode.Decompress);
        using var decompressedStream = new MemoryStream();
        await gZipStream.CopyToAsync(decompressedStream, cancellationToken);        
        return decompressedStream.ToArray();
    }
}
