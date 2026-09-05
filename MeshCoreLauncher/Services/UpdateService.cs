using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MeshCoreLauncher.Models;
using NLog;

namespace MeshCoreLauncher.Services;

public class UpdateService
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();
	private readonly ApiClient _api = new();
	private readonly PlatformInfo _platform = PlatformInfo.Current;

	public bool IsMeshCoreRunning()
	{
		var processName = Path.GetFileNameWithoutExtension(_platform.ExecutablePath);
		Process[] processes = Process.GetProcessesByName(processName);
		foreach (Process proc in processes) proc.Dispose();
		return processes.Length > 0;
	}

	public InstalledVersion? ReadInstalledVersion()
	{
		if (!File.Exists(_platform.VersionFilePath)) return null;

		try
		{
			return JsonSerializer.Deserialize<InstalledVersion>(File.ReadAllText(_platform.VersionFilePath));
		}
		catch
		{
			return null;
		}
	}

	public async Task<(bool UpdateAvailable, VersionInfo Remote, InstalledVersion? Installed, bool IsInstalled)> CheckAsync(CancellationToken ct = default)
	{
		VersionInfo remote = await _api.GetLatestAsync(_platform.ApiPlatformKey, ct);
		InstalledVersion? installed = ReadInstalledVersion();
		var isInstalled = File.Exists(_platform.ExecutablePath);
		var upToDate = installed is not null
		               && !string.IsNullOrEmpty(installed.Sha256)
		               && installed.Sha256.Equals(remote.Sha256, StringComparison.OrdinalIgnoreCase)
		               && isInstalled;

		return (!upToDate, remote, installed, isInstalled);
	}

	public async Task InstallAsync(VersionInfo remote, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
	{
		Log.Info("Installing MeshCore {0} (build {1}, commit {2})", remote.Version, remote.Build, remote.Commit);

		Directory.CreateDirectory(_platform.DownloadDir);
		var downloadPath = Path.Combine(_platform.DownloadDir, remote.Filename);

		if (TryVerifyChecksumQuiet(downloadPath, remote.Sha256))
		{
			progress?.Report(new DownloadProgress(remote.Size, remote.Size, 0));
		}
		else
		{
			await DownloadAsync(remote.Url, downloadPath, progress, ct);
			VerifyChecksum(downloadPath, remote.Sha256);
		}

		CloseRunningMeshCore();

		switch (_platform.Kind)
		{
			case InstallKind.Zip:
			case InstallKind.AppBundle:
				InstallFromZip(downloadPath);
				break;

			case InstallKind.AppImage:
				Directory.CreateDirectory(_platform.InstallDir);
				File.Move(downloadPath, _platform.ExecutablePath, true);
				MakeExecutable(_platform.ExecutablePath);
				break;
		}

		var installedVersion = new InstalledVersion
		{
			Version = remote.Version,
			Build = remote.Build,
			Commit = remote.Commit,
			Sha256 = remote.Sha256
		};
		File.WriteAllText(_platform.VersionFilePath, JsonSerializer.Serialize(installedVersion));
		Log.Info("MeshCore {0} installed successfully", remote.Version);
	}

	public void OpenAppFolder()
	{
		Directory.CreateDirectory(_platform.BaseDir);
		PlatformInfo.OpenWithDefaultApp(_platform.BaseDir);
	}

	public void OpenLogFile()
	{
		var logsDir = Path.Combine(_platform.BaseDir, "logs");
		var logPath = Path.Combine(logsDir, "launcher.log");
		Directory.CreateDirectory(logsDir);
		PlatformInfo.OpenWithDefaultApp(File.Exists(logPath) ? logPath : logsDir);
	}

	public void ClearDownloadCache()
	{
		if (Directory.Exists(_platform.DownloadDir))
			Directory.Delete(_platform.DownloadDir, true);
	}

	private void InstallFromZip(string downloadPath)
	{
		var stagingDir = _platform.InstallDir + ".staging";
		if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);

		ZipFile.ExtractToDirectory(downloadPath, stagingDir);

		var stagedExecutable = Path.Combine(stagingDir, Path.GetRelativePath(_platform.InstallDir, _platform.ExecutablePath));
		if (!File.Exists(stagedExecutable))
			throw new InvalidOperationException("Extracted package does not contain the expected executable.");
		MakeExecutable(stagedExecutable);
		File.Delete(downloadPath);

		var oldDir = _platform.InstallDir + ".old";
		if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true);
		if (Directory.Exists(_platform.InstallDir)) Directory.Move(_platform.InstallDir, oldDir);
		Directory.Move(stagingDir, _platform.InstallDir);
		if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true);
	}

	private void CloseRunningMeshCore()
	{
		var processName = Path.GetFileNameWithoutExtension(_platform.ExecutablePath);
		foreach (Process proc in Process.GetProcessesByName(processName))
		{
			try
			{
				if (!proc.CloseMainWindow() || !proc.WaitForExit(5000))
				{
					proc.Kill(true);
					proc.WaitForExit(3000);
				}
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Failed to close a running MeshCore process before installing an update");
			}
			finally
			{
				proc.Dispose();
			}
		}
	}

	public void Launch(bool forceAppImageExtractAndRun = false)
	{
		if (!File.Exists(_platform.ExecutablePath))
			throw new FileNotFoundException("MeshCore executable not found. Install it first.", _platform.ExecutablePath);

		if (_platform.Kind == InstallKind.AppBundle)
		{
			var appBundlePath = Path.Combine(_platform.InstallDir, "MeshCore.app");
			Process.Start(new ProcessStartInfo("open", ["-a", appBundlePath]) { UseShellExecute = false });
			return;
		}

		if (_platform.Kind == InstallKind.AppImage)
		{
			var startInfo = new ProcessStartInfo(_platform.ExecutablePath)
			{
				UseShellExecute = false,
				WorkingDirectory = _platform.InstallDir
			};
			if (forceAppImageExtractAndRun || !IsFuseAvailable())
				startInfo.ArgumentList.Add("--appimage-extract-and-run");

			Process.Start(startInfo);
			return;
		}

		Process.Start(new ProcessStartInfo(_platform.ExecutablePath)
		{
			UseShellExecute = true,
			WorkingDirectory = _platform.InstallDir
		});
	}

	public async Task<bool> LaunchAndConfirmAsync(bool forceAppImageExtractAndRun = false, CancellationToken ct = default)
	{
		Launch(forceAppImageExtractAndRun);
		await Task.Delay(1500, ct);
		return IsMeshCoreRunning();
	}

	public bool NeedsFuseInstall() => _platform.Kind == InstallKind.AppImage && !IsFuseAvailable();

	public string? FuseInstallDisplayCommand => DetectFuseInstallCommand()?.DisplayCommand;

	public async Task<bool> InstallFuseAsync(CancellationToken ct = default)
	{
		if (!File.Exists("/usr/bin/pkexec")) return false;

		(string[] Args, string DisplayCommand)? command = DetectFuseInstallCommand();
		if (command is null) return false;

		Log.Info("Installing FUSE via: pkexec {0}", command.Value.DisplayCommand);

		try
		{
			var startInfo = new ProcessStartInfo("pkexec") { UseShellExecute = false };
			foreach (var arg in command.Value.Args) startInfo.ArgumentList.Add(arg);

			using Process? process = Process.Start(startInfo);
			if (process is null) return false;

			await process.WaitForExitAsync(ct);
			return process.ExitCode == 0 && IsFuseAvailable();
		}
		catch (Exception ex)
		{
			Log.Warn(ex, "Failed to install FUSE automatically");
			return false;
		}
	}

	private static (string[] Args, string DisplayCommand)? DetectFuseInstallCommand()
	{
		if (File.Exists("/usr/bin/apt-get"))
			return (["apt-get", "install", "-y", "libfuse2"], "apt-get install -y libfuse2");
		if (File.Exists("/usr/bin/dnf"))
			return (["dnf", "install", "-y", "fuse-libs"], "dnf install -y fuse-libs");
		if (File.Exists("/usr/bin/yum"))
			return (["yum", "install", "-y", "fuse-libs"], "yum install -y fuse-libs");
		if (File.Exists("/usr/bin/pacman"))
			return (["pacman", "-S", "--noconfirm", "fuse2"], "pacman -S --noconfirm fuse2");
		if (File.Exists("/usr/bin/zypper"))
			return (["zypper", "install", "-y", "libfuse2"], "zypper install -y libfuse2");
		if (File.Exists("/sbin/apk") || File.Exists("/usr/bin/apk"))
			return (["apk", "add", "fuse"], "apk add fuse");
		return null;
	}

	private static readonly HttpClient DownloadClient = CreateDownloadClient();

	private static HttpClient CreateDownloadClient()
	{
		var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
		client.DefaultRequestHeaders.UserAgent.ParseAdd($"Mozilla/5.0 (compatible; MeshCoreLauncher/{version}; +https://meshcoreprofiles.com)");
		return client;
	}

	private static async Task DownloadAsync(string url, string destinationPath, IProgress<DownloadProgress>? progress, CancellationToken ct)
	{
		using HttpResponseMessage response = await DownloadClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
		response.EnsureSuccessStatusCode();

		var totalBytes = response.Content.Headers.ContentLength ?? -1L;
		await using Stream httpStream = await response.Content.ReadAsStreamAsync(ct);
		await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

		var buffer = new byte[81920];
		long totalRead = 0;
		var stopwatch = Stopwatch.StartNew();
		var lastReportElapsed = TimeSpan.Zero;
		long lastReportBytes = 0;
		int read;
		while ((read = await httpStream.ReadAsync(buffer, ct)) > 0)
		{
			await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
			totalRead += read;

			TimeSpan elapsed = stopwatch.Elapsed;
			TimeSpan sinceLastReport = elapsed - lastReportElapsed;
			if (sinceLastReport.TotalMilliseconds < 200) continue;

			var bytesPerSecond = sinceLastReport.TotalSeconds > 0 ? (totalRead - lastReportBytes) / sinceLastReport.TotalSeconds : 0;
			progress?.Report(new DownloadProgress(totalRead, totalBytes, bytesPerSecond));
			lastReportElapsed = elapsed;
			lastReportBytes = totalRead;
		}

		progress?.Report(new DownloadProgress(totalRead, totalBytes, 0));
	}

	private static string ComputeSha256(string filePath)
	{
		using var sha256 = SHA256.Create();
		using FileStream stream = File.OpenRead(filePath);
		return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
	}

	private static void VerifyChecksum(string filePath, string expectedSha256)
	{
		var actual = ComputeSha256(filePath);
		if (actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase)) return;

		File.Delete(filePath);
		throw new InvalidOperationException($"Checksum mismatch: expected {expectedSha256}, got {actual}. The download was discarded.");
	}

	private static bool TryVerifyChecksumQuiet(string filePath, string expectedSha256)
	{
		if (!File.Exists(filePath)) return false;

		try
		{
			return ComputeSha256(filePath).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static void MakeExecutable(string path)
	{
		if (OperatingSystem.IsWindows()) return;

		UnixFileMode mode = File.GetUnixFileMode(path);
		File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
	}

	private static bool IsFuseAvailable()
	{
		if (!NativeLibrary.TryLoad("libfuse.so.2", out var handle)) return false;

		NativeLibrary.Free(handle);
		return true;
	}
}
