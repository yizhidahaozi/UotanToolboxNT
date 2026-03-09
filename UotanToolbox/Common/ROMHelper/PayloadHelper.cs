using ChromeosUpdateEngine;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace UotanToolbox.Common.ROMHelper
{
    public class PayloadParser
    {
        private const string ZipMagic = "PK";
        private const string PayloadMagic = "CrAU";
        private const ulong BrilloMajorPayloadVersion = 2;
        private const string PayloadFilename = "payload.bin";

        /// <summary>
        /// 从 payload 文件中解压分区（可按分区名过滤）。
        /// </summary>
        public static void Extracet(string filepath, string[]? extractFiles = null)
        {
            if (extractFiles == null)
            {
                extractFiles = Array.Empty<string>();
            }
            FileStream f = File.OpenRead(filepath);
            {
                if (IsZip(f))
                {
                    // Extract payload.bin from the zip first
                    f.Close();
                    //$"Input is a zip file, searching for {PayloadFilename} ..."

                    using (ZipArchive zr = ZipFile.OpenRead(filepath))
                    {
                        ZipArchiveEntry zf = FindPayload(zr);
                        if (zf == null)
                        {
                            throw new Exception($"{PayloadFilename} not found in the zip file");
                        }

                        //$"Extracting {PayloadFilename} ..."
                        using (FileStream outFile = File.Create(PayloadFilename))
                        using (Stream zfStream = zf.Open())
                        {
                            zfStream.CopyTo(outFile);
                        }
                    }

                    f = File.OpenRead(PayloadFilename);
                }
                PayloadParser parser = new PayloadParser();
                parser.ParsePayload(f, extractFiles);
            }
        }

        /// <summary>
        /// 异步从 payload 文件中解压分区（可按分区名过滤）。
        /// </summary>
        public static async Task ExtracetAsync(string filepath, string[]? extractFiles = null)
        {
            if (extractFiles == null)
            {
                extractFiles = Array.Empty<string>();
            }
            FileStream f = File.OpenRead(filepath);
            {
                if (IsZip(f))
                {
                    f.Close();

                    using (ZipArchive zr = ZipFile.OpenRead(filepath))
                    {
                        ZipArchiveEntry zf = FindPayload(zr);
                        if (zf == null)
                        {
                            throw new Exception($"{PayloadFilename} not found in the zip file");
                        }

                        using (FileStream outFile = File.Create(PayloadFilename))
                        using (Stream zfStream = zf.Open())
                        {
                            await zfStream.CopyToAsync(outFile);
                        }
                    }

                    f = File.OpenRead(PayloadFilename);
                }
                PayloadParser parser = new PayloadParser();
                await parser.ParsePayloadAsync(f, extractFiles);

            }
        }

        /// <summary>
        /// 通过魔数判断流是否为 zip 文件。
        /// </summary>
        private static bool IsZip(FileStream f)
        {
            byte[] header = new byte[ZipMagic.Length];
            int bytesRead = f.Read(header, 0, header.Length);
            f.Seek(0, SeekOrigin.Begin);
            return bytesRead == header.Length && Encoding.ASCII.GetString(header) == ZipMagic;
        }

        /// <summary>
        /// 在 zip 包中查找 payload 条目。
        /// </summary>
        private static ZipArchiveEntry FindPayload(ZipArchive zr)
        {
            foreach (ZipArchiveEntry entry in zr.Entries)
            {
                if (entry.Name == PayloadFilename)
                {
                    return entry;
                }
            }
            return null;
        }

        public class PayloadMetadata
        {
            public uint BlockSize { get; set; }
            public List<string> PartitionNames { get; } = new List<string>();
            public ulong ManifestLength { get; set; }
            public uint MetadataSignatureLength { get; set; }
            public ulong BaseOffset { get; set; }
            public DeltaArchiveManifest Manifest { get; set; }
        }

        /// <summary>
        /// 不解压分区数据，读取 payload 元信息与分区列表。
        /// </summary>
        public static PayloadMetadata GetPayloadMetadata(string filepath)
        {
            PayloadParser parser = new PayloadParser();
            using (FileStream f = File.OpenRead(filepath))
            {
                if (IsZip(f))
                {
                    using (ZipArchive zr = ZipFile.OpenRead(filepath))
                    {
                        ZipArchiveEntry zf = FindPayload(zr);
                        if (zf == null)
                        {
                            throw new FileNotFoundException($"{PayloadFilename} not found in the zip file");
                        }

                        using (Stream zfStream = zf.Open())
                        {
                            return BuildMetadata(parser.ReadManifest(zfStream));
                        }
                    }
                }

                f.Seek(0, SeekOrigin.Begin);
                return BuildMetadata(parser.ReadManifest(f));
            }
        }

        /// <summary>
        /// 异步不解压分区数据，读取 payload 元信息与分区列表。
        /// </summary>
        public static async Task<PayloadMetadata> GetPayloadMetadataAsync(string filepath)
        {
            PayloadParser parser = new PayloadParser();
            using (FileStream f = File.OpenRead(filepath))
            {
                if (IsZip(f))
                {
                    using (ZipArchive zr = ZipFile.OpenRead(filepath))
                    {
                        ZipArchiveEntry zf = FindPayload(zr);
                        if (zf == null)
                        {
                            throw new FileNotFoundException($"{PayloadFilename} not found in the zip file");
                        }

                        using (Stream zfStream = zf.Open())
                        {
                            return BuildMetadata(await parser.ReadManifestAsync(zfStream));
                        }
                    }
                }

                f.Seek(0, SeekOrigin.Begin);
                return BuildMetadata(await parser.ReadManifestAsync(f));
            }
        }

        /// <summary>
        /// 仅解压指定分区。
        /// </summary>
        public static void ExtractSelectedPartitions(string filepath, string[]? partitionNames = null)
        {
            partitionNames ??= Array.Empty<string>();

            string payloadPath = filepath;
            string tempPath = null;

            using (FileStream fcheck = File.OpenRead(filepath))
            {
                if (IsZip(fcheck))
                {
                    using (ZipArchive zr = ZipFile.OpenRead(filepath))
                    {
                        ZipArchiveEntry zf = FindPayload(zr);
                        if (zf == null)
                        {
                            throw new FileNotFoundException($"{PayloadFilename} not found in the zip file");
                        }

                        tempPath = Path.GetTempFileName();
                        using (FileStream outFile = File.Create(tempPath))
                        using (Stream zfStream = zf.Open())
                        {
                            zfStream.CopyTo(outFile);
                        }
                        payloadPath = tempPath;
                    }
                }
            }

            try
            {
                using (FileStream stream = File.OpenRead(payloadPath))
                {
                    PayloadParser parser = new PayloadParser();
                    var (manifest, manifestLen, metadataSigLen, baseOffset) = parser.ReadManifest(stream);

                    IEnumerable<PartitionUpdate> partitions = manifest.Partitions.Where(p => p.PartitionName != null);
                    if (partitionNames.Length > 0)
                    {
                        partitions = partitions.Where(p => partitionNames.Contains(p.PartitionName));
                    }

                    foreach (var partition in partitions)
                    {
                        string outFilename = $"{partition.PartitionName}.img";
                        parser.ExtractPartition(partition, outFilename, stream, baseOffset, manifest.BlockSize);
                    }
                }
            }
            finally
            {
                if (tempPath != null)
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        /// <summary>
        /// 异步仅解压指定分区。
        /// </summary>
        public static async Task ExtractSelectedPartitionsAsync(string filepath, string[]? partitionNames = null)
        {
            partitionNames ??= Array.Empty<string>();

            string payloadPath = filepath;
            string tempPath = null;

            using (FileStream fcheck = File.OpenRead(filepath))
            {
                if (IsZip(fcheck))
                {
                    using (ZipArchive zr = ZipFile.OpenRead(filepath))
                    {
                        ZipArchiveEntry zf = FindPayload(zr);
                        if (zf == null)
                        {
                            throw new FileNotFoundException($"{PayloadFilename} not found in the zip file");
                        }

                        tempPath = Path.GetTempFileName();
                        using (FileStream outFile = File.Create(tempPath))
                        using (Stream zfStream = zf.Open())
                        {
                            await zfStream.CopyToAsync(outFile);
                        }
                        payloadPath = tempPath;
                    }
                }
            }

            try
            {
                using (FileStream stream = File.OpenRead(payloadPath))
                {
                    PayloadParser parser = new PayloadParser();
                    var (manifest, manifestLen, metadataSigLen, baseOffset) = await parser.ReadManifestAsync(stream);

                    IEnumerable<PartitionUpdate> partitions = manifest.Partitions.Where(p => p.PartitionName != null);
                    if (partitionNames.Length > 0)
                    {
                        partitions = partitions.Where(p => partitionNames.Contains(p.PartitionName));
                    }

                    var tasks = new List<Task>();
                    foreach (var partition in partitions)
                    {
                        string outFilename = $"{partition.PartitionName}.img";
                        tasks.Add(Task.Run(() =>
                        {
                            using (FileStream readStream = File.OpenRead(payloadPath))
                            {
                                parser.ExtractPartition(partition, outFilename, readStream, baseOffset, manifest.BlockSize);
                            }
                        }));
                    }
                    await Task.WhenAll(tasks);
                }
            }
            finally
            {
                if (tempPath != null)
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        /// <summary>
        /// 由解析后的 manifest 元组构建元信息对象。
        /// </summary>
        private static PayloadMetadata BuildMetadata((DeltaArchiveManifest manifest, ulong manifestLen, uint metadataSigLen, ulong baseOffset) data)
        {
            var (manifest, manifestLen, metadataSigLen, baseOffset) = data;
            var meta = new PayloadMetadata
            {
                BlockSize = manifest.BlockSize,
                ManifestLength = manifestLen,
                MetadataSignatureLength = metadataSigLen,
                BaseOffset = baseOffset,
                Manifest = manifest
            };

            foreach (var p in manifest.Partitions)
            {
                if (!string.IsNullOrEmpty(p.PartitionName))
                {
                    meta.PartitionNames.Add(p.PartitionName);
                }
            }

            return meta;
        }

        /// <summary>
        /// 从 payload 流读取并解析 manifest。
        /// </summary>
        private (DeltaArchiveManifest manifest, ulong manifestLen, uint metadataSigLen, ulong baseOffset) ReadManifest(Stream stream)
        {
            byte[] magic = new byte[PayloadMagic.Length];
            int bytesRead = stream.Read(magic, 0, magic.Length);
            if (bytesRead != magic.Length || Encoding.ASCII.GetString(magic) != PayloadMagic)
            {
                throw new InvalidOperationException($"Incorrect magic ({Encoding.ASCII.GetString(magic)})");
            }

            ulong version = ReadUInt64BigEndian(stream);
            if (version != BrilloMajorPayloadVersion)
            {
                throw new InvalidOperationException($"Unsupported payload version ({version}). This tool only supports version {BrilloMajorPayloadVersion}");
            }

            ulong manifestLen = ReadUInt64BigEndian(stream);
            if (manifestLen <= 0)
            {
                throw new InvalidOperationException($"Incorrect manifest length ({manifestLen})");
            }

            uint metadataSigLen = ReadUInt32BigEndian(stream);
            if (metadataSigLen <= 0)
            {
                throw new InvalidOperationException($"Incorrect metadata signature length ({metadataSigLen})");
            }

            byte[] manifestRaw = new byte[manifestLen];
            bytesRead = stream.Read(manifestRaw, 0, manifestRaw.Length);
            if ((ulong)bytesRead != manifestLen)
            {
                throw new InvalidOperationException($"Failed to read the manifest ({manifestLen})");
            }

            DeltaArchiveManifest manifest = DeltaArchiveManifest.Parser.ParseFrom(manifestRaw);
            if (manifest.MinorVersion != 0)
            {
                throw new InvalidOperationException("Delta payloads are not supported, please use a full payload file");
            }

            ulong baseOffset = 24 + manifestLen + metadataSigLen;
            return (manifest, manifestLen, metadataSigLen, baseOffset);
        }

        /// <summary>
        /// 异步从 payload 流读取并解析 manifest。
        /// </summary>
        private async Task<(DeltaArchiveManifest manifest, ulong manifestLen, uint metadataSigLen, ulong baseOffset)> ReadManifestAsync(Stream stream)
        {
            byte[] magic = new byte[PayloadMagic.Length];
            int bytesRead = await stream.ReadAsync(magic, 0, magic.Length);
            if (bytesRead != magic.Length || Encoding.ASCII.GetString(magic) != PayloadMagic)
            {
                throw new InvalidOperationException($"Incorrect magic ({Encoding.ASCII.GetString(magic)})");
            }

            ulong version = await ReadUInt64BigEndianAsync(stream);
            if (version != BrilloMajorPayloadVersion)
            {
                throw new InvalidOperationException($"Unsupported payload version ({version}). This tool only supports version {BrilloMajorPayloadVersion}");
            }

            ulong manifestLen = await ReadUInt64BigEndianAsync(stream);
            if (manifestLen <= 0)
            {
                throw new InvalidOperationException($"Incorrect manifest length ({manifestLen})");
            }

            uint metadataSigLen = await ReadUInt32BigEndianAsync(stream);
            if (metadataSigLen <= 0)
            {
                throw new InvalidOperationException($"Incorrect metadata signature length ({metadataSigLen})");
            }

            byte[] manifestRaw = new byte[manifestLen];
            bytesRead = await stream.ReadAsync(manifestRaw, 0, manifestRaw.Length);
            if ((ulong)bytesRead != manifestLen)
            {
                throw new InvalidOperationException($"Failed to read the manifest ({manifestLen})");
            }

            DeltaArchiveManifest manifest = DeltaArchiveManifest.Parser.ParseFrom(manifestRaw);
            if (manifest.MinorVersion != 0)
            {
                throw new InvalidOperationException("Delta payloads are not supported, please use a full payload file");
            }

            ulong baseOffset = 24 + manifestLen + metadataSigLen;
            return (manifest, manifestLen, metadataSigLen, baseOffset);
        }

        /// <summary>
        /// 解析 payload 并解压分区为镜像文件。
        /// </summary>
        private void ParsePayload(Stream stream, string[] extractFiles)
        {
            //"Parsing payload..."

            // Magic
            byte[] magic = new byte[PayloadMagic.Length];
            int bytesRead = stream.Read(magic, 0, magic.Length);
            if (bytesRead != magic.Length || Encoding.ASCII.GetString(magic) != PayloadMagic)
            {
                throw new InvalidOperationException($"Incorrect magic ({Encoding.ASCII.GetString(magic)})");
            }

            // Version & lengths
            ulong version = ReadUInt64BigEndian(stream);
            if (version != BrilloMajorPayloadVersion)
            {
                throw new InvalidOperationException($"Unsupported payload version ({version}). This tool only supports version {BrilloMajorPayloadVersion}");
            }

            ulong manifestLen = ReadUInt64BigEndian(stream);
            if (manifestLen <= 0)
            {
                throw new InvalidOperationException($"Incorrect manifest length ({manifestLen})");
            }

            uint metadataSigLen = ReadUInt32BigEndian(stream);
            if (metadataSigLen <= 0)
            {
                throw new InvalidOperationException($"Incorrect metadata signature length ({metadataSigLen})");
            }

            // Manifest
            byte[] manifestRaw = new byte[manifestLen];
            bytesRead = stream.Read(manifestRaw, 0, manifestRaw.Length);
            if ((ulong)bytesRead != manifestLen)
            {
                throw new InvalidOperationException($"Failed to read the manifest ({manifestLen})");
            }

            DeltaArchiveManifest manifest = DeltaArchiveManifest.Parser.ParseFrom(manifestRaw);
            if (manifest.MinorVersion != 0)
            {
                throw new InvalidOperationException("Delta payloads are not supported, please use a full payload file");
            }

            // Print manifest info
            //$"Block size: {manifest.BlockSize}, Partition count: {manifest.Partitions.Count}"

            // Extract partitions
            ExtractPartitions(manifest, stream, 24 + manifestLen + metadataSigLen, extractFiles);

            // Done
            //"Done!"
        }

        /// <summary>
        /// 从流读取大端 UInt64。
        /// </summary>
        private ulong ReadUInt64BigEndian(Stream stream)
        {
            byte[] buffer = new byte[8];
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new EndOfStreamException();
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            return BitConverter.ToUInt64(buffer, 0);
        }

        /// <summary>
        /// 从流读取大端 UInt32。
        /// </summary>
        private uint ReadUInt32BigEndian(Stream stream)
        {
            byte[] buffer = new byte[4];
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new EndOfStreamException();
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            return BitConverter.ToUInt32(buffer, 0);
        }
        /// <summary>
        /// 解压 manifest 中描述的分区。
        /// </summary>
        private void ExtractPartitions(DeltaArchiveManifest manifest, Stream stream, ulong baseOffset, string[] extractFiles)
        {
            foreach (var partition in manifest.Partitions)
            {
                if (partition.PartitionName == null || extractFiles.Length > 0 && !extractFiles.Contains(partition.PartitionName))
                {
                    continue;
                }
                //$"Extracting {partition.PartitionName} ({partition.Operations.Count} ops) ..."
                string outFilename = $"{partition.PartitionName}.img";
                ExtractPartition(partition, outFilename, stream, baseOffset, manifest.BlockSize);
            }
        }
        /// <summary>
        /// 异步解析 payload 并解压分区为镜像文件。
        /// </summary>
        public async Task ParsePayloadAsync(Stream stream, string[] extractFiles)
        {
            //"Parsing payload..."

            // Magic
            byte[] magic = new byte[PayloadMagic.Length];
            int bytesRead = await stream.ReadAsync(magic, 0, magic.Length);
            if (bytesRead != magic.Length || Encoding.ASCII.GetString(magic) != PayloadMagic)
            {
                throw new InvalidOperationException($"Incorrect magic ({Encoding.ASCII.GetString(magic)})");
            }

            // Version & lengths
            ulong version = await ReadUInt64BigEndianAsync(stream);
            if (version != BrilloMajorPayloadVersion)
            {
                throw new InvalidOperationException($"Unsupported payload version ({version}). This tool only supports version {BrilloMajorPayloadVersion}");
            }

            ulong manifestLen = await ReadUInt64BigEndianAsync(stream);
            if (manifestLen <= 0)
            {
                throw new InvalidOperationException($"Incorrect manifest length ({manifestLen})");
            }

            uint metadataSigLen = await ReadUInt32BigEndianAsync(stream);
            if (metadataSigLen <= 0)
            {
                throw new InvalidOperationException($"Incorrect metadata signature length ({metadataSigLen})");
            }

            // Manifest
            byte[] manifestRaw = new byte[manifestLen];
            bytesRead = await stream.ReadAsync(manifestRaw, 0, manifestRaw.Length);
            if ((ulong)bytesRead != manifestLen)
            {
                throw new InvalidOperationException($"Failed to read the manifest ({manifestLen})");
            }

            DeltaArchiveManifest manifest = DeltaArchiveManifest.Parser.ParseFrom(manifestRaw);
            if (manifest.MinorVersion != 0)
            {
                throw new InvalidOperationException("Delta payloads are not supported, please use a full payload file");
            }

            // Print manifest info
            //$"Block size: {manifest.BlockSize}, Partition count: {manifest.Partitions.Count}"

            // Extract partitions
            await ExtractPartitionsAsync(manifest, stream, 24 + manifestLen + metadataSigLen, extractFiles);

            // Done
        }

        /// <summary>
        /// 异步从流读取大端 UInt64。
        /// </summary>
        private async Task<ulong> ReadUInt64BigEndianAsync(Stream stream)
        {
            byte[] buffer = new byte[8];
            if (await stream.ReadAsync(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new EndOfStreamException();
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            return BitConverter.ToUInt64(buffer, 0);
        }

        /// <summary>
        /// 异步从流读取大端 UInt32。
        /// </summary>
        private async Task<uint> ReadUInt32BigEndianAsync(Stream stream)
        {
            byte[] buffer = new byte[4];
            if (await stream.ReadAsync(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new EndOfStreamException();
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            return BitConverter.ToUInt32(buffer, 0);
        }
        /// <summary>
        /// 异步解压 manifest 中描述的分区。
        /// </summary>
        public async Task ExtractPartitionsAsync(DeltaArchiveManifest manifest, Stream stream, ulong baseOffset, string[] extractFiles)
        {
            var tasks = new List<Task>();

            foreach (var partition in manifest.Partitions)
            {
                if (partition.PartitionName == null || extractFiles.Length > 0 && !extractFiles.Contains(partition.PartitionName))
                {
                    continue;
                }

                //$"Extracting {partition.PartitionName} ({partition.Operations.Count} ops) ..."
                string outFilename = $"{partition.PartitionName}.img";
                tasks.Add(Task.Run(() => ExtractPartition(partition, outFilename, stream, baseOffset, manifest.BlockSize)));
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 解压单个分区到镜像文件。
        /// </summary>
        private void ExtractPartition(PartitionUpdate partition, string outFilename, Stream stream, ulong baseOffset, uint blockSize)
        {
            using (var outFile = new FileStream(outFilename, FileMode.Create, FileAccess.Write))
            {
                foreach (var op in partition.Operations)
                {
                    byte[] data = new byte[op.DataLength];
                    long dataPos = (long)(baseOffset + op.DataOffset);

                    stream.Seek(dataPos, SeekOrigin.Begin);
                    int bytesRead = stream.Read(data, 0, data.Length);
                    if (bytesRead != data.Length)
                    {
                        throw new InvalidOperationException($"Failed to read enough data from partition {outFilename}");
                    }

                    long outSeekPos = (long)(op.DstExtents[0].StartBlock * blockSize);
                    outFile.Seek(outSeekPos, SeekOrigin.Begin);

                    switch (op.Type)
                    {
                        case InstallOperation.Types.Type.Replace:
                            outFile.Write(data, 0, data.Length);
                            break;

                        case InstallOperation.Types.Type.ReplaceBz:
                            using (var bzr = new BZip2Stream(new MemoryStream(data), SharpCompress.Compressors.CompressionMode.Decompress, false))
                            {
                                bzr.CopyTo(outFile);
                            }
                            break;

                        case InstallOperation.Types.Type.ReplaceXz:
                            using (var xzr = new XZStream(new MemoryStream(data)))
                            {
                                xzr.CopyTo(outFile);
                            }
                            break;

                        case InstallOperation.Types.Type.Zero:
                            foreach (var ext in op.DstExtents)
                            {
                                outSeekPos = (long)(ext.StartBlock * blockSize);
                                outFile.Seek(outSeekPos, SeekOrigin.Begin);
                                byte[] zeros = new byte[ext.NumBlocks * blockSize];
                                outFile.Write(zeros, 0, zeros.Length);
                            }
                            break;

                        default:
                            throw new InvalidOperationException($"Unsupported operation type: {op.Type}");
                    }
                }
            }
        }

        /// <summary>
        /// <summary>
        /// 检查远程 URL 是否包含 payload 内容。
        /// - ZIP 包：会尝试查找 payload.bin 条目
        /// - 直接的 payload.bin：只检查文件头魔数
        /// 方法不会下载整个文件，只使用范围请求读取必要字节。
        /// </summary>
        public static async Task<bool> RemoteUrlContainsPayloadAsync(string url)
        {
            using HttpClient client = new HttpClient();
            // fetch magic bytes (first four bytes enough for both zip and payload)
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new RangeHeaderValue(0, 3);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
                return false;
            byte[] header = await resp.Content.ReadAsByteArrayAsync();
            if (header.Length >= 2 && Encoding.ASCII.GetString(header, 0, 2) == ZipMagic)
            {
                // zip file, look for payload entry
                return await RemoteZipHasEntryAsync(client, url, PayloadFilename);
            }
            if (header.Length >= PayloadMagic.Length && Encoding.ASCII.GetString(header, 0, PayloadMagic.Length) == PayloadMagic)
            {
                // raw payload
                return true;
            }
            return false;
        }

        /// <summary>
        /// <summary>
        /// 从远程 URL 下载并解压 boot 分区镜像。
        /// - ZIP 包：仅拉取并解压 payload.bin 条目
        /// - 直接 payload.bin：下载整个文件
        /// 方法会根据元数据自动处理 A/B 多槽位，所有名称以 "boot" 开头的分区都会输出。
        /// </summary>
        public static async Task ExtractBootFromRemoteAsync(string url, string? outputDirectory = null)
        {
            outputDirectory ??= Directory.GetCurrentDirectory();

            using HttpClient client = new HttpClient();
            bool contains = await RemoteUrlContainsPayloadAsync(url);
            if (!contains)
                throw new FileNotFoundException("remote URL does not contain a recognized payload");

            string tempPayload = Path.GetTempFileName();
            try
            {
                bool isZip;
                // detect again for branch
                var headReq = new HttpRequestMessage(HttpMethod.Get, url);
                headReq.Headers.Range = new RangeHeaderValue(0, 3);
                using var headResp = await client.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead);
                byte[] header = await headResp.Content.ReadAsByteArrayAsync();
                isZip = header.Length >= 2 && Encoding.ASCII.GetString(header, 0, 2) == ZipMagic;

                if (isZip)
                {
                    await DownloadEntryFromZipAsync(client, url, PayloadFilename, tempPayload);
                }
                else
                {
                    // direct payload file, download whole thing
                    using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();
                    using var fs = File.Create(tempPayload);
                    await resp.Content.CopyToAsync(fs);
                }

                // metadata for boot partitions
                PayloadMetadata meta = isZip
                    ? GetPayloadMetadata(tempPayload)
                    : GetPayloadMetadata(tempPayload);
                var bootParts = meta.PartitionNames.Where(n => n.StartsWith("boot", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (bootParts.Length == 0)
                    throw new InvalidOperationException("no boot partitions found in payload");

                ExtractSelectedPartitions(tempPayload, bootParts);

                if (outputDirectory != Directory.GetCurrentDirectory())
                {
                    foreach (var part in bootParts)
                    {
                        string src = Path.Combine(Directory.GetCurrentDirectory(), part + ".img");
                        if (File.Exists(src))
                        {
                            File.Move(src, Path.Combine(outputDirectory, part + ".img"), true);
                        }
                    }
                }
            }
            finally
            {
                try { File.Delete(tempPayload); } catch { }
            }
        }

        private static async Task<bool> RemoteZipHasEntryAsync(HttpClient client, string url, string entryName)
        {
            // fetch end-of-central-directory by downloading last 66KB
            long fileSize = await GetRemoteFileSizeAsync(client, url);
            if (fileSize < 22)
                return false;

            long toFetch = Math.Min(66000, fileSize);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new RangeHeaderValue(fileSize - toFetch, fileSize - 1);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            byte[] tail = await resp.Content.ReadAsByteArrayAsync();
            int eocdOffset = FindEOCDOffset(tail);
            if (eocdOffset < 0)
                return false;
            // parse EOCD
            using var ms = new MemoryStream(tail, eocdOffset, tail.Length - eocdOffset);
            using var br = new BinaryReader(ms);
            br.ReadUInt32(); // signature
            br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16();
            uint centralSize = br.ReadUInt32();
            uint centralOffset = br.ReadUInt32();
            // now fetch central directory
            req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new RangeHeaderValue(centralOffset, centralOffset + centralSize - 1);
            using var cr = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            byte[] central = await cr.Content.ReadAsByteArrayAsync();
            return CentralDirectoryContains(central, entryName);
        }

        private static int FindEOCDOffset(byte[] data)
        {
            // search for 0x06054b50 backwards
            for (int i = data.Length - 22; i >= 0; i--)
            {
                if (data[i] == 0x50 && data[i + 1] == 0x4b && data[i + 2] == 0x05 && data[i + 3] == 0x06)
                    return i;
            }
            return -1;
        }

        private static bool CentralDirectoryContains(byte[] central, string entryName)
        {
            int offset = 0;
            while (offset + 46 <= central.Length)
            {
                uint sig = BitConverter.ToUInt32(central, offset);
                if (sig != 0x02014b50) break;
                ushort nameLen = BitConverter.ToUInt16(central, offset + 28);
                ushort extraLen = BitConverter.ToUInt16(central, offset + 30);
                ushort commentLen = BitConverter.ToUInt16(central, offset + 32);
                uint relOffset = BitConverter.ToUInt32(central, offset + 42);
                ushort compMethod = BitConverter.ToUInt16(central, offset + 10);
                uint compSize = BitConverter.ToUInt32(central, offset + 20);
                string name = Encoding.UTF8.GetString(central, offset + 46, nameLen);
                if (name == entryName)
                    return true;
                offset += 46 + nameLen + extraLen + commentLen;
            }
            return false;
        }

        private static async Task DownloadEntryFromZipAsync(HttpClient client, string url, string entryName, string outputPath)
        {
            // first locate entry with central directory again and record necessary info
            long fileSize = await GetRemoteFileSizeAsync(client, url);
            long toFetch = Math.Min(66000, fileSize);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new RangeHeaderValue(fileSize - toFetch, fileSize - 1);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            byte[] tail = await resp.Content.ReadAsByteArrayAsync();
            int eocdOffset = FindEOCDOffset(tail);
            if (eocdOffset < 0) throw new InvalidOperationException("EOCD not found");
            using var ms = new MemoryStream(tail, eocdOffset, tail.Length - eocdOffset);
            using var br = new BinaryReader(ms);
            br.ReadUInt32(); br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16();
            uint centralSize = br.ReadUInt32();
            uint centralOffset = br.ReadUInt32();
            req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new RangeHeaderValue(centralOffset, centralOffset + centralSize - 1);
            using var cr = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            byte[] central = await cr.Content.ReadAsByteArrayAsync();

            // find entry and gather info
            int offset = 0;
            uint localHeaderOffset = 0;
            ushort compMethod = 0;
            uint compSize = 0;
            while (offset + 46 <= central.Length)
            {
                uint sig = BitConverter.ToUInt32(central, offset);
                if (sig != 0x02014b50) break;
                ushort nameLen = BitConverter.ToUInt16(central, offset + 28);
                ushort extraLen = BitConverter.ToUInt16(central, offset + 30);
                ushort commentLen = BitConverter.ToUInt16(central, offset + 32);
                compMethod = BitConverter.ToUInt16(central, offset + 10);
                compSize = BitConverter.ToUInt32(central, offset + 20);
                localHeaderOffset = BitConverter.ToUInt32(central, offset + 42);
                string name = Encoding.UTF8.GetString(central, offset + 46, nameLen);
                if (name == entryName)
                {
                    break;
                }
                offset += 46 + nameLen + extraLen + commentLen;
            }
            if (localHeaderOffset == 0) throw new FileNotFoundException(entryName);

            // read local header to calculate data start
            req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new RangeHeaderValue(localHeaderOffset, localHeaderOffset + 64 * 1024 - 1); // enough to include header
            using var lr = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            byte[] localChunk = await lr.Content.ReadAsByteArrayAsync();
            using var lms = new MemoryStream(localChunk);
            using var lbr = new BinaryReader(lms);
            uint lhSig = lbr.ReadUInt32();
            if (lhSig != 0x04034b50) throw new InvalidOperationException("Invalid local header signature");
            lbr.ReadUInt16(); // version
            lbr.ReadUInt16(); // flag
            ushort lhCompMethod = lbr.ReadUInt16();
            lbr.ReadUInt16(); lbr.ReadUInt16();
            lbr.ReadUInt32(); lbr.ReadUInt32(); lbr.ReadUInt32();
            ushort fhNameLen = lbr.ReadUInt16();
            ushort fhExtraLen = lbr.ReadUInt16();
            long headerSize = 30 + fhNameLen + fhExtraLen;
            long dataOffset = (long)localHeaderOffset + headerSize;

            // now download compressed data only
            req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new RangeHeaderValue(dataOffset, dataOffset + compSize - 1);
            using var dr = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            using var compressedStream = await dr.Content.ReadAsStreamAsync();

            // decompress to outputPath
            using var outFs = File.Create(outputPath);
            Stream decompressStream;
            switch (compMethod)
            {
                case 0: // stored
                    await compressedStream.CopyToAsync(outFs);
                    return;
                case 8: // deflate
                    decompressStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported compression method {compMethod}");
            }
            await decompressStream.CopyToAsync(outFs);
        }

        private static async Task<long> GetRemoteFileSizeAsync(HttpClient client, string url)
        {
            var req = new HttpRequestMessage(HttpMethod.Head, url);
            var resp = await client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            if (resp.Content.Headers.ContentLength.HasValue)
                return resp.Content.Headers.ContentLength.Value;
            throw new InvalidOperationException("Unable to determine remote file size");
        }

        /// <summary>
        /// 从远程地址获取 payload 元数据。
        /// 用于查询分区表，不会下载整个 payload 文件。
        /// </summary>
        public static async Task<PayloadMetadata> GetPayloadMetadataFromUrlAsync(string url)
        {
            using HttpClient client = new HttpClient();
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new RangeHeaderValue(0, 3);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            byte[] header = await resp.Content.ReadAsByteArrayAsync();

            if (header.Length >= 2 && Encoding.ASCII.GetString(header, 0, 2) == ZipMagic)
            {
                bool has = await RemoteZipHasEntryAsync(client, url, PayloadFilename);
                if (!has)
                    throw new FileNotFoundException("payload.bin not found in remote zip");
                string tmp = Path.GetTempFileName();
                try
                {
                    await DownloadEntryFromZipAsync(client, url, PayloadFilename, tmp);
                    return GetPayloadMetadata(tmp);
                }
                finally { try { File.Delete(tmp); } catch { } }
            }
            else
            {
                // read header to get manifest length
                req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Range = new RangeHeaderValue(0, 23);
                using var hdrResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                hdrResp.EnsureSuccessStatusCode();
                byte[] hdr = await hdrResp.Content.ReadAsByteArrayAsync();
                if (hdr.Length < 24 || Encoding.ASCII.GetString(hdr, 0, PayloadMagic.Length) != PayloadMagic)
                    throw new InvalidOperationException("not a payload file");
                ulong version = ReadUInt64BigEndian(hdr, 4);
                if (version != BrilloMajorPayloadVersion)
                    throw new InvalidOperationException($"unsupported payload version {version}");
                ulong manifestLen = ReadUInt64BigEndian(hdr, 12);
                uint sigLen = ReadUInt32BigEndian(hdr, 20);
                long manifestStart = 24;
                long manifestEnd = manifestStart + (long)manifestLen - 1;
                req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Range = new RangeHeaderValue(manifestStart, manifestEnd);
                using var manResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                manResp.EnsureSuccessStatusCode();
                byte[] manifestRaw = await manResp.Content.ReadAsByteArrayAsync();
                DeltaArchiveManifest manifest = DeltaArchiveManifest.Parser.ParseFrom(manifestRaw);
                if (manifest.MinorVersion != 0)
                    throw new InvalidOperationException("Delta payloads are not supported, please use a full payload file");
                return BuildMetadata((manifest, manifestLen, sigLen, (ulong)(24 + manifestLen + sigLen)));
            }
        }

        // helpers for reading big-endian integers from byte arrays
        private static ulong ReadUInt64BigEndian(byte[] buf, int offset)
        {
            byte[] tmp = new byte[8];
            Array.Copy(buf, offset, tmp, 0, 8);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tmp);
            return BitConverter.ToUInt64(tmp, 0);
        }
        private static uint ReadUInt32BigEndian(byte[] buf, int offset)
        {
            byte[] tmp = new byte[4];
            Array.Copy(buf, offset, tmp, 0, 4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tmp);
            return BitConverter.ToUInt32(tmp, 0);
        }
    }
}
