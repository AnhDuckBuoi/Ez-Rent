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
        var extension = EZ_Rent.Services.ImageHeaderValidation.Validate(bytes);
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

}
