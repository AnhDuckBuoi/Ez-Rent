using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace EZ_Rent.Services;

public sealed class RentalFileStorageOptions
{
    public string? RootPath { get; set; }
}

public sealed record StoredRentalFile(string Key, string FileName, string ContentType, long Length, string Sha256);

/// <summary>Private local files. Only owner-scoped controllers expose their contents.</summary>
public sealed class RentalPrivateFileStore
{
    public const int DocumentMaxBytes = 10 * 1024 * 1024;
    public const int ImageMaxBytes = 5 * 1024 * 1024;
    private readonly string root;

    public RentalPrivateFileStore(IWebHostEnvironment environment, IOptions<RentalFileStorageOptions> options)
    {
        root = Path.GetFullPath(options.Value.RootPath ?? Path.Combine("App_Data", "RentalFiles"), environment.ContentRootPath);
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        if (root.Equals(webRoot, StringComparison.OrdinalIgnoreCase) ||
            root.StartsWith(webRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Kho tài liệu phải nằm ngoài wwwroot.");
    }

    public async Task<StoredRentalFile> SaveAsync(IFormFile file, bool allowPdf, CancellationToken ct = default)
    {
        var maxBytes = allowPdf ? DocumentMaxBytes : ImageMaxBytes;
        if (file.Length is <= 0 || file.Length > maxBytes)
            throw new InvalidDataException($"Tệp cần có dung lượng từ 1 byte đến {maxBytes / 1024 / 1024} MB.");
        await using var source = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes) throw new InvalidDataException("Tệp vượt quá dung lượng cho phép.");
            await buffer.WriteAsync(block.AsMemory(0, read), ct);
        }
        var bytes = buffer.ToArray();
        var suppliedExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string extension, contentType;
        if (allowPdf && suppliedExtension == ".pdf" && bytes.AsSpan().StartsWith("%PDF-"u8) &&
            System.Text.Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 1024))).Contains("%%EOF", StringComparison.Ordinal))
        {
            extension = ".pdf"; contentType = "application/pdf";
        }
        else
        {
            extension = ImageHeaderValidation.Validate(bytes);
            if (suppliedExtension != extension && !(extension == ".jpg" && suppliedExtension == ".jpeg"))
                throw new InvalidDataException("Phần mở rộng không khớp với định dạng ảnh.");
            contentType = extension == ".png" ? "image/png" : "image/jpeg";
        }
        var key = $"{Guid.NewGuid():N}{extension}";
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, key);
        try { await File.WriteAllBytesAsync(path, bytes, ct); }
        catch { File.Delete(path); throw; }
        var name = file.FileName.Replace('\\', '/').Split('/').Last();
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        if (string.IsNullOrWhiteSpace(name)) name = "tai-lieu" + extension;
        if (name.Length > 180) name = name[..(180 - extension.Length)] + extension;
        return new(key, name, contentType, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public FileStream? OpenRead(string key)
    {
        if (!IsValidKey(key)) return null;
        try { return new FileStream(Path.Combine(root, key), FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void DeleteUncommitted(string key)
    {
        if (IsValidKey(key)) File.Delete(Path.Combine(root, key));
    }

    private static bool IsValidKey(string key) => Regex.IsMatch(key, @"\A[a-f0-9]{32}\.(pdf|png|jpg)\z", RegexOptions.CultureInvariant);
}
