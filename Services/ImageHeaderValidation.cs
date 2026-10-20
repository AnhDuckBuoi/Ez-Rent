namespace EZ_Rent.Services;

// Header checks shared with avatar uploads; not a full image decoder or malware scanner.
public static class ImageHeaderValidation
{
    public static string Validate(byte[] bytes)
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
