using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace EZ_Rent.Data.Seeder;

/// <summary>
/// Tệp minh họa cho dữ liệu mẫu chủ trọ, sinh bằng code (không cần ảnh có sẵn, không cần thư viện vẽ):
/// - MeterPng: ảnh màn hình công tơ dạng LED 7 đoạn hiển thị đúng chỉ số của phiếu (PNG hợp lệ, qua ImageHeaderValidation);
/// - ContractPdf: PDF một trang tóm tắt hợp đồng (chữ không dấu vì dùng font chuẩn Helvetica của PDF).
/// </summary>
internal static class LandlordDemoImages
{
    private const int Width = 520, Height = 220;

    public static byte[] MeterPng(double index, bool water)
    {
        var px = new byte[Width * Height * 3];
        void Fill(int x, int y, int w, int h, (byte R, byte G, byte B) c)
        {
            for (var yy = Math.Max(0, y); yy < Math.Min(Height, y + h); yy++)
                for (var xx = Math.Max(0, x); xx < Math.Min(Width, x + w); xx++)
                {
                    var i = (yy * Width + xx) * 3;
                    px[i] = c.R; px[i + 1] = c.G; px[i + 2] = c.B;
                }
        }
        var background = water ? ((byte)224, (byte)242, (byte)254) : ((byte)254, (byte)243, (byte)199);
        var band = water ? ((byte)14, (byte)165, (byte)233) : ((byte)245, (byte)158, (byte)11);
        var lit = water ? ((byte)56, (byte)189, (byte)248) : ((byte)251, (byte)191, (byte)36);
        (byte, byte, byte) dim = (31, 41, 55), panel = (17, 24, 39), frame = (75, 85, 99);
        Fill(0, 0, Width, Height, background);
        Fill(0, 0, Width, 18, band);
        Fill(24, 44, Width - 48, 140, frame);
        Fill(30, 50, Width - 60, 128, panel);

        var text = index.ToString(water ? "0000.0" : "00000.0", CultureInfo.InvariantCulture);
        const int cell = 52, digitH = 90, t = 9, m = 8, dot = 18;
        var totalW = text.Sum(ch => ch == '.' ? dot : cell);
        var x0 = 30 + (Width - 60 - totalW) / 2;
        const int y0 = 69;
        var segments = new Dictionary<char, string>
        {
            ['0'] = "abcdef", ['1'] = "bc", ['2'] = "abged", ['3'] = "abgcd", ['4'] = "fgbc",
            ['5'] = "afgcd", ['6'] = "afgecd", ['7'] = "abc", ['8'] = "abcdefg", ['9'] = "abcdfg"
        };
        foreach (var ch in text)
        {
            if (ch == '.') { Fill(x0 + 4, y0 + digitH - t, t, t, lit); x0 += dot; continue; }
            var on = segments[ch];
            var half = digitH / 2;
            var hw = cell - 2 * m - 2 * t;
            var vh = half - t - t / 2;
            void Seg(char s, int x, int y, int w, int h) => Fill(x, y, w, h, on.Contains(s) ? lit : dim);
            Seg('a', x0 + m + t, y0, hw, t);
            Seg('g', x0 + m + t, y0 + half - t / 2, hw, t);
            Seg('d', x0 + m + t, y0 + digitH - t, hw, t);
            Seg('f', x0 + m, y0 + t, t, vh);
            Seg('b', x0 + cell - m - t, y0 + t, t, vh);
            Seg('e', x0 + m, y0 + half + t / 2, t, vh);
            Seg('c', x0 + cell - m - t, y0 + half + t / 2, t, vh);
            x0 += cell;
        }
        return EncodePng(px);
    }

    private static byte[] EncodePng(byte[] rgb)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < Height; y++)
        {
            raw.WriteByte(0); // bộ lọc "None" cho mỗi dòng
            raw.Write(rgb, y * Width * 3, Width * 3);
        }
        byte[] compressed;
        using (var z = new MemoryStream())
        {
            using (var zlib = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) raw.WriteTo(zlib);
            compressed = z.ToArray();
        }
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, Width); WriteBigEndian(ihdr, 4, Height);
        ihdr[8] = 8; ihdr[9] = 2; // 8 bit, RGB
        Chunk(png, "IHDR", ihdr);
        Chunk(png, "IDAT", compressed);
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBigEndian(len, 0, data.Length); s.Write(len);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes); s.Write(data);
        var crc = Crc32(typeBytes.Concat(data).ToArray());
        var c = new byte[4]; WriteBigEndian(c, 0, unchecked((int)crc)); s.Write(c);
    }

    private static void WriteBigEndian(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc32(byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    /// <summary>PDF 1 trang (A4). Mỗi dòng là chữ ASCII; ký tự ( ) \ được thoát theo chuẩn PDF.</summary>
    public static byte[] ContractPdf(IReadOnlyList<string> lines)
    {
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var content = new StringBuilder();
        var y = 780;
        for (var i = 0; i < lines.Count; i++)
        {
            var size = i == 0 ? 16 : 11;
            content.Append(CultureInfo.InvariantCulture, $"BT /F1 {size} Tf 64 {y} Td ({Esc(lines[i])}) Tj ET\n");
            y -= i == 0 ? 30 : 18;
        }
        var stream = content.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) pdf.Append(CultureInfo.InvariantCulture, $"{o:D10} 00000 n \n");
        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
