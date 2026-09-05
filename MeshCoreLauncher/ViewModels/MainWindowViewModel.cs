using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeshCoreLauncher.Models;
using MeshCoreLauncher.Services;
using NLog;

namespace MeshCoreLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();
	private readonly CancellationTokenSource _autoRecheckCts = new();
	private readonly UpdateService _updateService = new();
	[ObservableProperty] private bool _actionButtonEnabled;
	[ObservableProperty] private string _actionButtonText = "Please wait";
	[ObservableProperty] private string _detailText = "";
	[ObservableProperty] private string _downloadInfoText = "";
	[ObservableProperty] private bool _isBusy;
	[ObservableProperty] private bool _isProgressVisible;
	[ObservableProperty] private bool _isToastVisible;
	private CancellationTokenSource? _installCts;
	private PendingAction _pendingAction = PendingAction.None;

	private VersionInfo? _pendingRemote;
	[ObservableProperty] private double _progress;
	private int _toastGeneration;

	[ObservableProperty] private StatusKind _statusKind = StatusKind.Checking;
	[ObservableProperty] private string _statusText = "Checking for updates...";
	[ObservableProperty] private string _toastMessage = "";

	public MainWindowViewModel(SettingsService settings)
	{
		Settings = settings;

		_ = InitializeAsync();
		_ = RunAutoRecheckLoopAsync(_autoRecheckCts.Token);
	}

	public SettingsService Settings { get; }
	public string SystemText { get; } = $"{PlatformInfo.Current.DisplayName} - {RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";

	public event EventHandler? MinimizeToTrayRequested;
	public event EventHandler? OpenSettingsRequested;
	public event EventHandler? ExitRequested;
	public event Func<string, Task<bool>>? ConfirmationRequested;

	private async Task InitializeAsync()
	{
		await CheckForUpdatesAsync();

		if (Settings.Current.AutoLaunchMeshCoreOnStartup)
			await LaunchAsync();
	}

	private async Task RunAutoRecheckLoopAsync(CancellationToken token)
	{
		try
		{
			while (!token.IsCancellationRequested)
			{
				TimeSpan interval = TimeSpan.FromMinutes(Math.Max(1, Settings.Current.AutoRecheckIntervalMinutes));
				await Task.Delay(interval, token);

				if (!Settings.Current.AutoRecheckForUpdatesEnabled || IsBusy) continue;
				await CheckForUpdatesAsync();
			}
		}
		catch (TaskCanceledException)
		{
			// loop cancelled on exit
		}
	}

	[RelayCommand]
	private async Task CheckForUpdatesAsync()
	{
		if (IsBusy) return;

		IsBusy = true;
		if (_pendingRemote is null) ActionButtonEnabled = false;
		StatusKind = StatusKind.Checking;
		StatusText = "Checking for updates...";

		try
		{
			(var updateAvailable, VersionInfo remote, InstalledVersion? installed, var isInstalled) = await _updateService.CheckAsync();
			_pendingRemote = remote;
			_pendingAction = !updateAvailable ? PendingAction.Launch : !isInstalled ? PendingAction.Install : PendingAction.Update;

			StatusKind = StatusKindFor(_pendingAction);
			StatusText = _pendingAction switch
			{
				PendingAction.Install => $"Install available: {remote.Version}",
				PendingAction.Update => $"Update available: {installed?.Version ?? "unknown"} -> {remote.Version}",
				_ => $"Up to date: {remote.Version}"
			};

			var shortCommit = remote.Commit.Length > 7 ? remote.Commit[..7] : remote.Commit;
			DetailText = $"build {remote.Build} ({shortCommit})";
			ActionButtonText = _pendingAction switch
			{
				PendingAction.Install => "Install & Launch",
				PendingAction.Update => "Update & Launch",
				_ => "Launch"
			};
			ActionButtonEnabled = true;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to check for updates");
			StatusKind = StatusKind.Error;
			StatusText = $"Failed to check for updates: {ex.Message}";
			DetailText = "";
			ActionButtonText = "Retry";
			ActionButtonEnabled = true;
			ShowToast($"Could not check for updates: {ex.Message}");
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task LaunchAsync()
	{
		if (_pendingRemote is null)
		{
			await CheckForUpdatesAsync();
			return;
		}

		ActionButtonEnabled = false;

		try
		{
			if (_pendingAction is PendingAction.Install or PendingAction.Update)
			{
				var isFreshInstall = _pendingAction == PendingAction.Install;

				if (Settings.Current.AskBeforeClosingMeshCore && _updateService.IsMeshCoreRunning())
				{
					var confirmed = ConfirmationRequested is null
					                || await ConfirmationRequested.Invoke("MeshCore is currently running and needs to be closed to install the update. Close it and continue?");
					if (!confirmed)
					{
						StatusKind = StatusKindFor(_pendingAction);
						StatusText = "Update postponed - MeshCore is still running.";
						return;
					}
				}

				IsBusy = true;
				IsProgressVisible = true;
				StatusKind = StatusKind.Installing;
				StatusText = isFreshInstall ? $"Downloading {_pendingRemote.Version}..." : $"Downloading update {_pendingRemote.Version}...";

				_installCts = new CancellationTokenSource();
				var progress = new Progress<DownloadProgress>(p =>
				{
					Progress = p.PercentComplete;
					DownloadInfoText = FormatDownloadInfo(p);
				});
				await _updateService.InstallAsync(_pendingRemote, progress, _installCts.Token);

				StatusText = isFreshInstall ? $"Installed {_pendingRemote.Version}. Launching..." : $"Updated to {_pendingRemote.Version}. Launching...";
				ShowToast(isFreshInstall ? $"MeshCore {_pendingRemote.Version} installed successfully." : $"MeshCore updated to {_pendingRemote.Version}.");

				_pendingAction = PendingAction.Launch;
				ActionButtonText = "Launch";
			}
			else if (_updateService.IsMeshCoreRunning())
			{
				StatusKind = StatusKind.UpToDate;
				StatusText = "MeshCore is already running.";
				ShowToast("MeshCore is already running.");

				HandlePostLaunch();
				return;
			}
			else
			{
				StatusText = "Launching...";
			}

			if (!Settings.Current.ForceAppImageExtractAndRun && _updateService.NeedsFuseInstall())
				await OfferFuseInstallAsync();

			IsBusy = true;
			var launched = await _updateService.LaunchAndConfirmAsync(Settings.Current.ForceAppImageExtractAndRun);

			if (launched)
			{
				StatusKind = StatusKind.UpToDate;
				StatusText = $"Up to date: {_pendingRemote.Version}";
				HandlePostLaunch();
			}
			else
			{
				StatusKind = StatusKind.Error;
				StatusText = "MeshCore exited immediately after launching.";
				ShowToast("MeshCore did not start. Check the log file for details.");
			}
		}
		catch (OperationCanceledException)
		{
			StatusKind = StatusKindFor(_pendingAction);
			StatusText = "Cancelled.";
			ShowToast("Download cancelled.");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Launch/install failed");
			StatusKind = StatusKind.Error;
			StatusText = $"Failed: {ex.Message}";
			ShowToast($"Operation failed: {ex.Message}");
		}
		finally
		{
			IsProgressVisible = false;
			DownloadInfoText = "";
			_installCts?.Dispose();
			_installCts = null;
			IsBusy = false;
			ActionButtonEnabled = true;
		}
	}

	[RelayCommand]
	private void CancelInstall()
	{
		_installCts?.Cancel();
	}

	private async Task OfferFuseInstallAsync()
	{
		var installCommand = _updateService.FuseInstallDisplayCommand;
		var prompt = installCommand is not null
			? $"MeshCore needs FUSE to run, which isn't installed. Install it now?\n\n{installCommand}"
			: "MeshCore needs FUSE to run, which isn't installed, and it could not be detected automatically for this distribution. Install it manually, or enable \"Skip FUSE, always extract AppImage\" in Settings.";

		var confirmed = ConfirmationRequested is not null && await ConfirmationRequested.Invoke(prompt);
		if (!confirmed) return;

		StatusText = "Installing FUSE...";
		var installed = await _updateService.InstallFuseAsync();
		if (!installed)
			ShowToast("Could not install FUSE automatically. MeshCore will start in a slower fallback mode.");
	}

	private void HandlePostLaunch()
	{
		if (Settings.Current.ExitAfterLaunch)
		{
			_autoRecheckCts.Cancel();
			ExitRequested?.Invoke(this, EventArgs.Empty);
		}
		else if (Settings.Current.MinimizeToTrayAfterLaunch)
		{
			MinimizeToTrayRequested?.Invoke(this, EventArgs.Empty);
		}
	}

	private static StatusKind StatusKindFor(PendingAction action) => action switch
	{
		PendingAction.Install => StatusKind.InstallAvailable,
		PendingAction.Update => StatusKind.UpdateAvailable,
		_ => StatusKind.UpToDate
	};

	private static string FormatDownloadInfo(DownloadProgress p)
	{
		var received = FormatSize(p.BytesReceived);
		var total = p.TotalBytes > 0 ? FormatSize(p.TotalBytes) : "?";
		var speed = FormatSize((long)p.BytesPerSecond);
		return $"{p.PercentComplete:0}% - {received} / {total} - {speed}/s";
	}

	private static string FormatSize(long bytes)
	{
		string[] units = ["B", "KB", "MB", "GB"];
		double size = bytes;
		var unitIndex = 0;
		while (size >= 1024 && unitIndex < units.Length - 1)
		{
			size /= 1024;
			unitIndex++;
		}

		return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.0} {units[unitIndex]}";
	}

	[RelayCommand]
	private void OpenAppFolder()
	{
		_updateService.OpenAppFolder();
	}

	[RelayCommand]
	private void OpenSettings()
	{
		OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
	}

	[RelayCommand]
	private void Exit()
	{
		_autoRecheckCts.Cancel();
		ExitRequested?.Invoke(this, EventArgs.Empty);
	}

	private async void ShowToast(string message)
	{
		var generation = ++_toastGeneration;
		ToastMessage = message;
		IsToastVisible = true;
		await Task.Delay(3200);
		if (generation == _toastGeneration) IsToastVisible = false;
	}
}
