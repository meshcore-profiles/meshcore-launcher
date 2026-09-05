using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MeshCoreLauncher.Services;

public enum InstallKind
{
	Zip,
	AppImage,
	AppBundle
}

public class PlatformInfo
{

	private PlatformInfo(string apiPlatformKey, InstallKind kind, string baseDir, string executableRelativePath)
	{
		ApiPlatformKey = apiPlatformKey;
		Kind = kind;
		BaseDir = baseDir;
		InstallDir = Path.Combine(baseDir, "app");
		ExecutablePath = Path.Combine(InstallDir, executableRelativePath);
		VersionFilePath = Path.Combine(baseDir, "version.json");
		DownloadDir = Path.Combine(baseDir, "downloads");
	}

	public string ApiPlatformKey { get; }
	public InstallKind Kind { get; }
	public string BaseDir { get; }
	public string InstallDir { get; }
	public string ExecutablePath { get; }
	public string VersionFilePath { get; }
	public string DownloadDir { get; }

	public string DisplayName => Kind switch
	{
		InstallKind.Zip => "Windows",
		InstallKind.AppBundle => "macOS",
		InstallKind.AppImage => "Linux (AppImage)",
		_ => ApiPlatformKey
	};

	public static PlatformInfo Current { get; } = Detect();

	public static void OpenWithDefaultApp(string path)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			Process.Start(new ProcessStartInfo("open", [path]) { UseShellExecute = false });
		else
			Process.Start(new ProcessStartInfo("xdg-open", [path]) { UseShellExecute = false });
	}

	private static PlatformInfo Detect()
	{
		var baseDir = GetBaseDataDir();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return new PlatformInfo("windows", InstallKind.Zip, baseDir, "MeshCore.exe");

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			return new PlatformInfo("macos", InstallKind.AppBundle, baseDir, Path.Combine("MeshCore.app", "Contents", "MacOS", "MeshCore"));

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			return new PlatformInfo("linux-appimage", InstallKind.AppImage, baseDir, "MeshCore.AppImage");

		throw new PlatformNotSupportedException("MeshCore Launcher does not support this operating system.");
	}

	private static string GetBaseDataDir()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeshCoreLauncher");

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "MeshCoreLauncher");

		var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
		var dataHome = string.IsNullOrEmpty(xdgDataHome)
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
			: xdgDataHome;
		return Path.Combine(dataHome, "MeshCoreLauncher");
	}
}
