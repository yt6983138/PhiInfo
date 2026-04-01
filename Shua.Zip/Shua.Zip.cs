using System.IO.Compression;

namespace Shua.Zip;

public sealed class ShuaZip : IDisposable
{
	private readonly IReadAt _reader;
	private readonly long _size;
	public List<FileEntry> FileEntries { get; }
	private bool _disposed;

	public ShuaZip(IReadAt reader)
	{
		this._reader = reader;
		this._size = reader.Size;
		this.FileEntries = this.LoadCentralDirectory();
	}

	private List<FileEntry> LoadCentralDirectory()
	{
		EndOfCentralDirectory eocd = this.FindEocd();

		if (eocd.CentralDirectorySize > int.MaxValue)
		{
			throw new InvalidOperationException("Central directory too large");
		}

		byte[] cdData = this.ReadAt((long)eocd.CentralDirectoryOffset, (int)eocd.CentralDirectorySize);

		return this.ParseCentralDirectory(cdData);
	}

	private List<FileEntry> ParseCentralDirectory(byte[] cdData)
	{
		List<FileEntry> entries = [];
		int position = 0;

		while (position + 4 <= cdData.Length)
		{
			if (!FileEntry.TryReadFromCentralDirectory(cdData, ref position, out FileEntry? entry))
			{
				break;
			}

			if (entry != null)
			{
				entries.Add(entry);
			}
		}

		return entries;
	}

	private EndOfCentralDirectory FindEocd()
	{
		long searchStart = Math.Max(0, this._size - (65535 + 22));
		long searchEnd = this._size;

		for (long offset = searchEnd - 1; offset >= searchStart; offset--)
		{
			if (searchEnd - offset < 22)
			{
				continue;
			}

			byte[] data = this.ReadAt(offset, 22);

			if (data.Length >= 4
				&& data[0] == 0x50
				&& data[1] == 0x4B
				&& data[2] == 0x05
				&& data[3] == 0x06)
			{
				int commentLen = data[20] | (data[21] << 8);
				if (offset + 22 + commentLen == this._size)
				{
					return EndOfCentralDirectory.FromEocd(this._reader, offset, data);
				}
			}
		}

		throw new InvalidOperationException("End of Central Directory not found");
	}

	public byte[] ReadFile(FileEntry entry)
	{
		using Stream stream = this.OpenFileStream(entry);
		int capacity = 0;
		if (entry.CompressionMethod.IsStored)
		{
			capacity = (int)entry.CompressedSize;
		}
		else if (entry.CompressionMethod.IsDeflate)
		{
			capacity = (int)entry.UncompressedSize;
		}

		using MemoryStream output = new(capacity);
		stream.CopyTo(output);
		return output.ToArray();
	}

	public Stream OpenFileStream(FileEntry entry)
	{
		if (entry.LocalHeaderOffset > long.MaxValue)
		{
			throw new InvalidOperationException("Local header offset too large");
		}

		byte[] headerData = this.ReadAt((long)entry.LocalHeaderOffset, 30);

		if (headerData.Length < 30
			|| headerData[0] != 0x50
			|| headerData[1] != 0x4B
			|| headerData[2] != 0x03
			|| headerData[3] != 0x04)
		{
			throw new InvalidOperationException("Invalid local file header signature");
		}

		int filenameLen = headerData[26] | (headerData[27] << 8);
		int extraLen = headerData[28] | (headerData[29] << 8);

		long dataOffset = (long)entry.LocalHeaderOffset + 30L + filenameLen + extraLen;

		if (entry.CompressedSize > int.MaxValue)
		{
			throw new InvalidOperationException("Compressed size too large");
		}

		Stream rawStream = this._reader.OpenRead(dataOffset, (int)entry.CompressedSize);

		if (entry.CompressionMethod.IsStored)
		{
			return rawStream;
		}

		if (entry.CompressionMethod.IsDeflate)
		{
			if (entry.UncompressedSize > int.MaxValue)
				throw new InvalidOperationException("Uncompressed size too large");

			using DeflateStream deflateStream = new(rawStream, CompressionMode.Decompress, leaveOpen: false);
			MemoryStream memory = new((int)entry.UncompressedSize);
			deflateStream.CopyTo(memory);
			memory.Position = 0;
			return memory;
		}

		rawStream.Dispose();
		throw new InvalidOperationException($"Unsupported compression method: {entry.CompressionMethod.Value}");
	}

	private byte[] ReadAt(long offset, int length)
	{
		if (length == 0)
		{
			return [];
		}

		using Stream stream = this._reader.OpenRead(offset, length);
		byte[] buffer = new byte[length];
		int readTotal = 0;

		while (readTotal < length)
		{
			int read = stream.Read(buffer, readTotal, length - readTotal);
			if (read <= 0)
			{
				throw new EndOfStreamException("Unexpected end of stream");
			}

			readTotal += read;
		}

		return buffer;
	}

	public Stream OpenFileStreamByName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			throw new ArgumentNullException(nameof(fileName));

		FileEntry entry = this.FileEntries.Find(f => string.Equals(f.Name, fileName, StringComparison.OrdinalIgnoreCase)) ??
						throw new FileNotFoundException($"File '{fileName}' not found in the archive.");

		return this.OpenFileStream(entry);
	}


	public byte[] ReadFileByName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			throw new ArgumentNullException(nameof(fileName));

		FileEntry entry = this.FileEntries.Find(f => string.Equals(f.Name, fileName, StringComparison.OrdinalIgnoreCase)) ??
						throw new FileNotFoundException($"File '{fileName}' not found in the archive.");

		return this.ReadFile(entry);
	}

	public void Dispose()
	{
		if (this._disposed) return;
		this._disposed = true;
		this._reader.Dispose();
	}
}