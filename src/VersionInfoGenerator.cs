using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Chronofoil.CaptureFile.Generated;
using Google.Protobuf;

namespace Chronofoil.CaptureFile;

public static class VersionInfoGenerator
{
	/// <summary>
	/// Generates a VersionInfo provided a game path.
	/// </summary>
	/// <param name="gamePath"></param>
	/// <returns></returns>
	public static VersionInfo Generate(string gamePath, string writerId, string writerVersion)
	{
		var ret = new VersionInfo();
		var path = Process.GetCurrentProcess().MainModule.FileName;
		var dx11Path = path.Replace("ffxiv.exe", "ffxiv_dx11.exe");
		var dx9Path = path.Replace("ffxiv_dx11.exe", "ffxiv.exe");

		var dx9Data = File.Exists(dx9Path) ? File.ReadAllBytes(dx9Path) : Array.Empty<byte>();
		var dx11Data = File.ReadAllBytes(dx11Path);

		var parent = Directory.GetParent(path).FullName;
		var sqpack = Path.Combine(parent, "sqpack");
		
		var ffxivVerFile = Path.Combine(parent, "ffxivgame.ver");

		if (File.Exists(dx9Path))
		{
			ret.Dx9Revision = GetBuild(dx9Data);
			ret.Dx9Hash = ByteString.CopyFrom(GetHash(dx9Data));
		}
			
		if (File.Exists(dx11Path))
		{
			ret.Dx11Revision = GetBuild(dx11Data);
			ret.Dx11Hash = ByteString.CopyFrom(GetHash(dx11Data));
		}

		ret.GameVer.Add(GetVer(ffxivVerFile));

		for (int i = 1;; i++)
		{
			var exVerFile = Path.Combine(sqpack, $"ex{i}", $"ex{i}.ver");
			if (!File.Exists(exVerFile)) break;
			var ver = GetVer(exVerFile);
			ret.GameVer.Add(ver);
		}

		ret.WriterIdentifier = writerId;
		ret.WriterVersion = writerVersion;
		
		return ret;
	}
	
	private static long GetBuild(byte[] data)
	{
		var bytes = "/*****ff14******rev"u8.ToArray();
		var stringBytes = new List<byte>();
		for (int i = 0; i < data.Length - bytes.Length; i++) {
			if (data.AsSpan().Slice(i, bytes.Length).SequenceEqual(bytes))
			{
				i += bytes.Length;
				for (int j = 0; data[i + j] != '_'; j++) {
					stringBytes.Add(data[i + j]);
				}
				break;
			}
		}
		return long.Parse(Encoding.ASCII.GetString(stringBytes.ToArray()));
	}

	private static byte[] GetHash(byte[] data)
	{
		return SHA1.HashData(data);
	}

	private static string GetVer(string path)
	{
		return File.Exists(path) ? File.ReadAllText(path) : "0000.00.00.0000.0000";
	}
}