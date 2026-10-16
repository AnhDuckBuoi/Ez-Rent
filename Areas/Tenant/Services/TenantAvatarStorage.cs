namespace EZ_Rent.Areas.Tenant.Services;

/// <summary>Stores only small PNG/JPEG avatars under a random, application-generated name.</summary>
public static class TenantAvatarStorage
{
    public const int MaxBytes = 2 * 1024 * 1024;
    private const string UrlPrefix = "/uploads/avatars/";

    public static async Task<string> SaveAsync(IFormFile file, string webRoot, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaxBytes)
            throw new InvalidDataException("Ảnh cần có dung lượng từ 1 byte đến 2 MB.");

        // Bound the actual read as well as the client-provided length.
        await using var source = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new InvalidDataException("Ảnh vượt quá dung lượng 2 MB.");
            await buffer.WriteAsync(block.AsMemory(0, read), ct);
        }
        var bytes = buffer.ToArray();
        var extension = ValidateImage(bytes);
        var directory = Path.Combine(webRoot, "uploads", "avatars");
        Directory.CreateDirectory(directory);
        var name = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(directory, name);
        try { await File.WriteAllBytesAsync(path, bytes, ct); }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        return UrlPrefix + name;
    }

    public static void DeleteLocal(string? url, string webRoot)
    {
        if (url == null || !url.StartsWith(UrlPrefix, StringComparison.Ordinal)) return;
        var name = url[UrlPrefix.Length..];
        var extension = Path.GetExtension(name);
        // Never turn an arbitrary URL or request filename into a filesystem path.
        if (extension is not (".png" or ".jpg") || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _) ||
            name.Contains('/') || name.Contains('\\')) return;
        File.Delete(Path.Combine(webRoot, "uploads", "avatars", name));
    }

    private static string ValidateImage(byte[] bytes)
    {
        if (bytes.Length >= 45 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) && bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8))
        {
            var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
            var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
            CheckDimensions(width, height);
            return ".png";
        }
        if (bytes.Length >= 12 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[^2] == 0xff && bytes[^1] == 0xd9)
        {
            // JPEG stores dimensions in a start-of-frame segment, not at a fixed offset.
            var position = 2;
            while (position + 3 < bytes.Length)
            {
                if (bytes[position++] != 0xff) break;
                while (position < bytes.Length && bytes[position] == 0xff) position++;
                if (position >= bytes.Length) break;
                var marker = bytes[position++];
                if (marker is 0xd9 or 0xda) break;
                if (marker is 0xd8 or 0x01 || marker is >= 0xd0 and <= 0xd7) continue;
                if (position + 2 > bytes.Length) break;
                var length = (bytes[position] << 8) + bytes[position + 1];
                if (length < 2 || position + length > bytes.Length) break;
                if ((marker is 0xc0 or 0xc1 or 0xc2) && length >= 8)
                {
                    var height = (uint)((bytes[position + 3] << 8) + bytes[position + 4]);
                    var width = (uint)((bytes[position + 5] << 8) + bytes[position + 6]);
                    CheckDimensions(width, height);
                    return ".jpg";
                }
                position += length;
            }
        }
        throw new InvalidDataException("Chọn ảnh PNG hoặc JPEG hợp lệ. Ảnh SVG và các loại tệp khác chưa được hỗ trợ.");
    }

    private static void CheckDimensions(uint width, uint height)
    {
        if (width is 0 or > 4096 || height is 0 or > 4096)
            throw new InvalidDataException("Mỗi chiều ảnh cần từ 1 đến 4.096 pixel.");
    }
}
