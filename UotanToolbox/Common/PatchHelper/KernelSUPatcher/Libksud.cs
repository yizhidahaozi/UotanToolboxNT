using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace UotanToolbox.Common.PatchHelper.KernelSUPatcher
{
    public class LibksudAsset
    {
        public string Name { get; init; } = string.Empty;
        public int Offset { get; init; }
        public int CompressedSize { get; init; }
        public int DecompressedSize { get; init; }
        public uint Crc32 { get; init; }
        public string Sha256 { get; init; } = string.Empty;

        private readonly byte[] _fullData;

        internal LibksudAsset(byte[] fullData, string name, int offset, int compressedSize, int decompressedSize, uint crc32, string sha256)
        {
            _fullData = fullData;
            Name = name;
            Offset = offset;
            CompressedSize = compressedSize;
            DecompressedSize = decompressedSize;
            Crc32 = crc32;
            Sha256 = sha256;
        }

        public byte[] Decompress()
        {
            using var ms = new MemoryStream(_fullData, Offset, CompressedSize, writable: false);
            using var deflate = new SharpCompress.Compressors.Deflate.DeflateStream(ms, SharpCompress.Compressors.CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                outMs.Write(buffer, 0, read);
            return outMs.ToArray();
        }

        public void Export(string outputPath)
        {
            var data = Decompress();
            File.WriteAllBytes(outputPath, data);
        }
    }

    public class Libksud(byte[] data)
    {
        private readonly byte[] _data = data;
        private List<LibksudAsset>? _assets;

        public static Libksud LoadFromFile(string path)
        {
            return new Libksud(File.ReadAllBytes(path));
        }

        public static Libksud LoadFromStream(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return new Libksud(ms.ToArray());
        }

        public IReadOnlyList<LibksudAsset> GetAssets()
        {
            if (_assets == null)
            {
                _assets = ParseAssets();
                // 重点修复：即使 ScanZlibAssets 扫描到了二进制块，但由于 RustEmbed 解析失败，
                // 这些块会被命名为 asset_XX.bin。我们需要根据字符串扫描结果强制纠正这些名称。
                bool hasKsu = _assets.Any(a => a.Name.EndsWith("_kernelsu.ko"));
                if (!hasKsu)
                {
                    string text = Encoding.ASCII.GetString(_data);
                    var matches = Regex.Matches(text, @"(android\d+-\d+\.\d+_kernelsu\.ko|ksuinit|ksud|resetprop|busybox|bootctl)");
                    if (matches.Count > 0)
                    {
                        var updatedList = new List<LibksudAsset>();
                        int matchIdx = 0;
                        foreach (var asset in _assets)
                        {
                            if (asset.Name.StartsWith("asset_") && matchIdx < matches.Count)
                            {
                                updatedList.Add(new LibksudAsset(
                                    _data, matches[matchIdx++].Value, asset.Offset,
                                    asset.CompressedSize, 0, 0, ""
                                ));
                            }
                            else
                            {
                                updatedList.Add(asset);
                            }
                        }
                        _assets = updatedList;
                    }
                }
            }
            return _assets;
        }

        public List<string> GetSupportedKMI()
        {
            var assets = GetAssets();
            var kmis = new List<string>();
            foreach (var asset in assets)
            {
                if (asset.Name.EndsWith("_kernelsu.ko"))
                {
                    string kmi = asset.Name.Replace("_kernelsu.ko", "");
                    if (!kmis.Contains(kmi))
                        kmis.Add(kmi);
                }
            }

            // 最后的保底逻辑：如果 GetAssets 没拿到，直接暴力扫描二进制数据中存在的 KMI 字符串
            if (kmis.Count == 0)
            {
                // 搜索 android\d+-\d+\.\d+ 这类典型的 KMI 字符串
                string text = Encoding.ASCII.GetString(_data);
                var matches = Regex.Matches(text, @"android\d+-\d+\.\d+(?=_kernelsu\.ko)");
                foreach (Match m in matches.Cast<Match>())
                {
                    if (!kmis.Contains(m.Value))
                        kmis.Add(m.Value);
                }
            }
            return kmis;
        }

        private List<LibksudAsset> ParseAssets()
        {
            var rawAssets = ScanZlibAssets(_data);
            var indexEntries = ParseRustEmbed(_data);
            var names = BuildAssetNames(_data, indexEntries, rawAssets.Count);

            var results = new List<LibksudAsset>(rawAssets.Count);
            for (int i = 0; i < rawAssets.Count; i++)
            {
                var asset = rawAssets[i];
                var name = names.ElementAtOrDefault(i) ?? $"asset_{i:00}.bin";

                // Decompress head of asset to confirm its identity if name is missing or generic
                if (name.StartsWith("asset_"))
                {
                    try
                    {
                        using var ms = new MemoryStream(_data, asset.Offset, asset.CompressedSize, writable: false);
                        using var deflate = new SharpCompress.Compressors.Deflate.DeflateStream(ms, SharpCompress.Compressors.CompressionMode.Decompress);
                        var buffer = new byte[128];
                        int read = deflate.Read(buffer, 0, buffer.Length);
                        if (read > 0)
                        {
                            string head = Encoding.ASCII.GetString(buffer, 0, Math.Min(read, 64));
                            // If it's the ksuinit script (usually starts with #!/bin/sh)
                            if (head.Contains("#!/bin/sh")) name = "ksuinit";
                        }
                    }
                    catch { }
                }

                results.Add(new LibksudAsset(
                    _data,
                    name,
                    asset.Offset,
                    asset.CompressedSize,
                    0,
                    0,
                    ""
                ));
            }
            return results;
        }

        private static List<InternalAssetInfo> ScanZlibAssets(byte[] data)
        {
            var result = new List<InternalAssetInfo>();
            for (int i = 0; i < data.Length - 16; i++)
            {
                // 检查 Zlib 流魔数 0x78 0x01/0x9C/0xDA 或 RFC1950 校验
                if (data[i] == 0x78 && (data[i + 1] == 0x01 || data[i + 1] == 0x9c || data[i + 1] == 0xda))
                {
                    if (TryDecompressRawDeflate(data, i, out int decompressedLen, out var consumed))
                    {
                        result.Add(new InternalAssetInfo
                        {
                            Offset = i,
                            CompressedSize = consumed
                        });
                        // 跳过已处理的压缩块，防止重叠扫描
                        if (consumed > 0) i += (consumed - 1);
                    }
                }
            }
            return result;
        }

        private static bool TryDecompressRawDeflate(byte[] data, int offset, out int decompressedLen, out int consumed)
        {
            decompressedLen = 0;
            consumed = 0;
            try
            {
                using var ms = new MemoryStream(data, offset, data.Length - offset, writable: false);
                using var deflate = new SharpCompress.Compressors.Deflate.DeflateStream(ms, SharpCompress.Compressors.CompressionMode.Decompress, SharpCompress.Compressors.Deflate.CompressionLevel.BestSpeed, Encoding.ASCII);
                var header = new byte[4];
                int read = deflate.Read(header, 0, 4);

                if (read == 4)
                {
                    // Check for ELF magic: 0x7f 'E' 'L' 'F'
                    // Reference implementation uses this to confirm it's a valid asset block
                    if (header[0] == 0x7f && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F')
                    {
                        // Consume some more to get TotalIn updated
                        var buffer = new byte[8192];
                        while (deflate.Read(buffer, 0, buffer.Length) > 0) ;

                        consumed = (int)deflate.TotalIn;
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private static bool TryInflateHeader(byte[] data, int offset)
        {
            byte b0 = data[offset];
            byte b1 = data[offset + 1];
            if (b0 != 0x78) return false;
            if ((b0 * 256 + b1) % 31 != 0) return false;
            return true;
        }

        private static List<InternalIndexEntry> ParseRustEmbed(byte[] data)
        {
            var entries = new List<InternalIndexEntry>();
            var refStr = Encoding.ASCII.GetBytes("_kernelsu.ko");
            var span = data.AsSpan();
            int firstMatch = span.IndexOf(refStr);
            if (firstMatch < 0) return entries;
            int refOff = firstMatch;
            while (refOff > 0 && data[refOff - 1] != 0 && (refOff - firstMatch) > -64)
            {
                refOff--;
            }
            var refPtr = BitConverter.GetBytes((ulong)refOff);
            int tablePos = -1;
            // 搜索引用该字符串地址的索引表，由于索引表可能在 refOff 之后，所以搜索范围扩大
            for (int i = 0; i < data.Length - 8; i += 8)
            {
                if (data.AsSpan(i, 8).SequenceEqual(refPtr))
                {
                    tablePos = i;
                    // 向上找索引表的开头
                    while (tablePos >= 24)
                    {
                        var prevPtr = BitConverter.ToUInt64(data, tablePos - 24);
                        if (prevPtr > 0 && prevPtr < (ulong)data.Length && data[(int)prevPtr] >= 0x20)
                            tablePos -= 24;
                        else
                            break;
                    }
                    if (tablePos >= 0) break;
                }
            }

            if (tablePos < 0) return entries;

            for (int offset = tablePos; offset + 24 <= data.Length && entries.Count < 500; offset += 24)
            {
                var namePtr = BitConverter.ToUInt64(data, offset);
                // In rust-embed 0.8+, there's a 0x403 constant (or similar) at offset + 16
                // Our reference code uses that to validate the index entry

                if (namePtr <= 0 || namePtr >= (ulong)data.Length) break;

                int nameMax = Math.Min(256, data.Length - (int)namePtr);
                int actualLen = 0;
                while (actualLen < nameMax && data[(int)namePtr + actualLen] != 0 && data[(int)namePtr + actualLen] >= 0x20)
                {
                    actualLen++;
                }

                if (actualLen < 4) break;

                string name = Encoding.ASCII.GetString(data, (int)namePtr, actualLen);

                // Align with reference regex: ^(android\d+-\d+\.\d+_kernelsu\.ko|bootctl|busybox|resetprop|ksud|ksuinit)
                if (Regex.IsMatch(name, "^(android\\d+-\\d+\\.\\d+_kernelsu\\.ko|bootctl|busybox|resetprop|ksud|ksuinit)"))
                {
                    entries.Add(new InternalIndexEntry { Name = name });
                }
                else
                {
                    // If we encounter a name that doesn't match our assets, we've likely hit the end of the table
                    // but we allow a few non-matching entries if they look like strings, 
                    // though for libksud specifically the list is usually clean.
                    if (!name.Contains('.') && !name.Contains('/') && !name.Contains('_')) break;
                    entries.Add(new InternalIndexEntry { Name = name });
                }
            }
            return entries;
        }

        private static List<string> BuildAssetNames(byte[] data, List<InternalIndexEntry> indexEntries, int expectedCount)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in indexEntries)
            {
                if (!string.IsNullOrEmpty(e.Name) && seen.Add(e.Name))
                    names.Add(e.Name);
            }

            if (data.AsSpan().IndexOf(Encoding.ASCII.GetBytes("ksud")) >= 0 && seen.Add("ksud"))
            {
                names.Add("ksud");
            }

            if (names.Count == 0 || !names.Any(n => n.EndsWith("_kernelsu.ko")))
            {
                // 增大扫描范围：KMI 字符串可能分布在 libksud 的只读数据区各处
                // 搜索 android[version]-[kernel_version]_kernelsu.ko
                int searchSize = Math.Min(data.Length, 2 * 1024 * 1024);
                var textSnippet = Encoding.ASCII.GetString(data, Math.Max(0, data.Length - searchSize), searchSize);
                var koMatches = Regex.Matches(textSnippet, "android\\d+-\\d+\\.\\d+_kernelsu\\.ko");
                foreach (Match m in koMatches.Cast<Match>())
                {
                    if (seen.Add(m.Value)) names.Add(m.Value);
                }

                if (textSnippet.Contains("ksuinit") && seen.Add("ksuinit"))
                    names.Add("ksuinit");
                if (textSnippet.Contains("ksud") && seen.Add("ksud"))
                    names.Add("ksud");
            }

            while (names.Count < expectedCount)
            {
                var placeholder = $"asset_{names.Count:00}.bin";
                if (seen.Add(placeholder)) names.Add(placeholder);
            }

            return names.Take(expectedCount).ToList();
        }

        private static byte[] DecompressRawDeflate(ArraySegment<byte> compressed)
        {
            if (compressed.Array == null) return Array.Empty<byte>();
            using var ms = new MemoryStream(compressed.Array, compressed.Offset, compressed.Count, writable: false);
            using var deflate = new SharpCompress.Compressors.Deflate.DeflateStream(ms, SharpCompress.Compressors.CompressionMode.Decompress, SharpCompress.Compressors.Deflate.CompressionLevel.BestSpeed, Encoding.ASCII);
            using var outMs = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                outMs.Write(buffer, 0, read);
            return outMs.ToArray();
        }

        private static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        private static uint CalculateCrc32(byte[] data)
        {
            const uint poly = 0xEDB88320;
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
            {
                var cur = (crc ^ b) & 0xFF;
                for (int i = 0; i < 8; i++)
                    cur = (cur & 1) != 0 ? (cur >> 1) ^ poly : cur >> 1;
                crc = (crc >> 8) ^ cur;
            }
            return ~crc;
        }

        private static int IndexOf(byte[] data, byte[] needle)
        {
            for (int i = 0; i <= data.Length - needle.Length; i++)
                if (data.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
            return -1;
        }

        private class InternalAssetInfo
        {
            public int Offset { get; set; }
            public int CompressedSize { get; set; }
        }

        private class InternalIndexEntry
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
