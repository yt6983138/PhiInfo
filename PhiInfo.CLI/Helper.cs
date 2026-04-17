using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace PhiInfo.CLI;
internal static class Helper
{
	internal static T EnsureNotNull<T>(this T? value, string? paramName = null) where T : class
	{
		return value ?? throw new ArgumentNullException(paramName);
	}
	/// <summary>
	/// intended for short byte arrays
	/// </summary>
	/// <param name="bytes"></param>
	/// <returns></returns>
	internal static string ToHexString(this byte[] bytes)
	{
		Span<char> str = stackalloc char[bytes.Length * 2];

		for (int i = 0; i < bytes.Length; i++)
		{
			Span<char> slice = str.Slice(i * 2, 2);
			bytes[i].TryFormat(slice, out int charsWritten, "x2");
		}

		return new(str);
	}
	internal static string EnsureAssetCanCreate(this string outputPath, bool isFile = true)
	{
		if (isFile)
		{
			string? parentDir = Path.GetDirectoryName(outputPath);
			ArgumentException.ThrowIfNullOrEmpty(parentDir);
			Directory.CreateDirectory(parentDir);
		}
		else
		{
			Directory.CreateDirectory(outputPath);
		}
		return outputPath;
	}

	extension(CsvWriter self)
	{
		public static CsvWriter FromStringBuilder(StringBuilder sb, string delimiter)
		{
			CsvConfiguration config = new(CultureInfo.InvariantCulture)
			{
				Delimiter = delimiter
			};
			return new CsvWriter(new StringWriter(sb), config);
		}
		public void WriteFields(params IEnumerable<string> strings)
		{
			foreach (string item in strings)
			{
				self.WriteField(item);
			}
		}
	}
}
