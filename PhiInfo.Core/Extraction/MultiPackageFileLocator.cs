using System.IO.Compression;

namespace PhiInfo.Core.Extraction;

/// <summary>
/// Manages multiple package archives (APK, OBB, split APKs) and provides unified file lookup.
/// Handles proper disposal of all underlying ZipArchive instances.
/// Searches through packages in the order provided, returning the first match found.
/// </summary>
public class MultiPackageFileLocator : IDisposable
{
	private readonly List<ZipArchive> _archives = [];
	private readonly SemaphoreSlim _lock = new(1, 1);
	private bool _disposed;

	/// <summary>
	/// Creates a new instance that manages multiple package streams.
	/// </summary>
	/// <param name="packages">Package streams (APK, OBB, split APKs). Will be opened as ZipArchives. The streams will not be disposed by this class.</param>
	/// <exception cref="ArgumentException">Thrown if no packages are provided.</exception>
	public MultiPackageFileLocator(params Stream[] packages)
	{
		if (packages.Length == 0)
			throw new ArgumentException("At least one package must be provided.", nameof(packages));

		foreach (Stream package in packages)
		{
			this._archives.Add(new(package, ZipArchiveMode.Read, leaveOpen: true));
		}
	}

	/// <summary>
	/// Searches for a file entry across all packages. Returns the first match found.
	/// </summary>
	/// <param name="path">The path to the file within the package (e.g., "assets/aa/catalog.json").</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The ZipArchiveEntry if found, otherwise null.</returns>
	public async Task<ZipArchiveEntry?> TryGetEntryAsync(string path, CancellationToken ct = default)
	{
		await this._lock.WaitAsync(ct);
		try
		{
			foreach (ZipArchive archive in this._archives)
			{
				ZipArchiveEntry? entry = archive.GetEntry(path);
				if (entry is not null)
					return entry;
			}
			return null;
		}
		finally
		{
			this._lock.Release();
		}
	}

	/// <summary>
	/// Searches for a file entry across all packages. Throws if not found.
	/// </summary>
	/// <param name="path">The path to the file within the package (e.g., "assets/aa/catalog.json").</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The ZipArchiveEntry.</returns>
	/// <exception cref="FileNotFoundException">Thrown if the file is not found in any package.</exception>
	public async Task<ZipArchiveEntry> GetEntryOrThrowAsync(string path, CancellationToken ct = default)
	{
		ZipArchiveEntry? entry = await this.TryGetEntryAsync(path, ct);
		if (entry is null)
		{
			throw new FileNotFoundException(
				$"Required Unity asset missing from all {this._archives.Count} package(s): {path}");
		}

		return entry;
	}

	/// <summary>
	/// Gets all entries matching a specific prefix across all packages.
	/// Useful for finding split files like "sharedassets22.assets.split*".
	/// </summary>
	/// <param name="prefix">The prefix to match (e.g., "assets/bin/Data/sharedassets22.assets.split").</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A list of matching entries from all packages.</returns>
	public async Task<List<ZipArchiveEntry>> GetEntriesWithPrefixAsync(string prefix, CancellationToken ct = default)
	{
		await this._lock.WaitAsync(ct);
		try
		{
			List<ZipArchiveEntry> matches = [];
			foreach (ZipArchive archive in this._archives)
			{
				foreach (ZipArchiveEntry entry in archive.Entries)
				{
					if (entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
						matches.Add(entry);
				}
			}
			return matches;
		}
		finally
		{
			this._lock.Release();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref this._disposed, true)) return;

		GC.SuppressFinalize(this);

		foreach (ZipArchive archive in this._archives)
		{
			archive.Dispose();
		}
		this._lock.Dispose();
	}
}
