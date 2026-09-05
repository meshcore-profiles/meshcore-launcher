using System;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeshCoreLauncher.Models;
using MeshCoreLauncher.Services;

namespace MeshCoreLauncher.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
	private readonly AutostartService _autostart = new();
	private readonly SettingsService _settingsService;
	private readonly UpdateService _updateService = new();
	private bool _isInitializing;

	[ObservableProperty] private bool _askBeforeClosingMeshCore;
	[ObservableProperty] private bool _autoLaunchMeshCoreOnStartup;
	[ObservableProperty] private bool _autoRecheckForUpdatesEnabled;
	[ObservableProperty] private int _autoRecheckIntervalMinutes;
	[ObservableProperty] private bool _exitAfterLaunch;
	[ObservableProperty] private bool _forceAppImageExtractAndRun;
	[ObservableProperty] private bool _launchOnSystemStartup;
	[ObservableProperty] private bool _minimizeToTrayAfterLaunch;
	[ObservableProperty] private bool _minimizeToTrayOnClose;
	[ObservableProperty] private bool _notifyOnUpdateAvailable;
	[ObservableProperty] private bool _startMinimizedToTray;

	public SettingsWindowViewModel(SettingsService settingsService)
	{
		_settingsService = settingsService;
		LoadFrom(_settingsService.Current);
	}

	public event EventHandler? CloseRequested;

	public bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

	private void LoadFrom(AppSettings s)
	{
		_isInitializing = true;
		LaunchOnSystemStartup = s.LaunchOnSystemStartup;
		StartMinimizedToTray = s.StartMinimizedToTray;
		AutoLaunchMeshCoreOnStartup = s.AutoLaunchMeshCoreOnStartup;
		MinimizeToTrayOnClose = s.MinimizeToTrayOnClose;
		MinimizeToTrayAfterLaunch = s.MinimizeToTrayAfterLaunch;
		ExitAfterLaunch = s.ExitAfterLaunch;
		AskBeforeClosingMeshCore = s.AskBeforeClosingMeshCore;
		NotifyOnUpdateAvailable = s.NotifyOnUpdateAvailable;
		AutoRecheckForUpdatesEnabled = s.AutoRecheckForUpdatesEnabled;
		AutoRecheckIntervalMinutes = s.AutoRecheckIntervalMinutes;
		ForceAppImageExtractAndRun = s.ForceAppImageExtractAndRun;
		_isInitializing = false;
	}

	partial void OnLaunchOnSystemStartupChanged(bool value)
	{
		Persist();
		if (_isInitializing) return;
		_autostart.SetEnabled(value, StartMinimizedToTray);
	}

	partial void OnStartMinimizedToTrayChanged(bool value)
	{
		Persist();
		if (_isInitializing || !LaunchOnSystemStartup) return;
		_autostart.SetEnabled(true, value);
	}

	partial void OnAutoLaunchMeshCoreOnStartupChanged(bool value)
	{
		Persist();
	}

	partial void OnMinimizeToTrayOnCloseChanged(bool value)
	{
		Persist();
	}

	partial void OnMinimizeToTrayAfterLaunchChanged(bool value)
	{
		Persist();
	}

	partial void OnExitAfterLaunchChanged(bool value)
	{
		Persist();
	}

	partial void OnAskBeforeClosingMeshCoreChanged(bool value)
	{
		Persist();
	}

	partial void OnNotifyOnUpdateAvailableChanged(bool value)
	{
		Persist();
	}

	partial void OnAutoRecheckForUpdatesEnabledChanged(bool value)
	{
		Persist();
	}

	partial void OnAutoRecheckIntervalMinutesChanged(int value)
	{
		if (value < 1) AutoRecheckIntervalMinutes = 1;
		Persist();
	}

	partial void OnForceAppImageExtractAndRunChanged(bool value)
	{
		Persist();
	}

	private void Persist()
	{
		if (_isInitializing) return;

		AppSettings s = _settingsService.Current;
		s.LaunchOnSystemStartup = LaunchOnSystemStartup;
		s.StartMinimizedToTray = StartMinimizedToTray;
		s.AutoLaunchMeshCoreOnStartup = AutoLaunchMeshCoreOnStartup;
		s.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
		s.MinimizeToTrayAfterLaunch = MinimizeToTrayAfterLaunch;
		s.ExitAfterLaunch = ExitAfterLaunch;
		s.AskBeforeClosingMeshCore = AskBeforeClosingMeshCore;
		s.NotifyOnUpdateAvailable = NotifyOnUpdateAvailable;
		s.AutoRecheckForUpdatesEnabled = AutoRecheckForUpdatesEnabled;
		s.AutoRecheckIntervalMinutes = AutoRecheckIntervalMinutes;
		s.ForceAppImageExtractAndRun = ForceAppImageExtractAndRun;
		_settingsService.Save();
	}

	[RelayCommand]
	private void ResetToDefaults()
	{
		LoadFrom(new AppSettings());
		Persist();
		_autostart.SetEnabled(LaunchOnSystemStartup, StartMinimizedToTray);
	}

	[RelayCommand]
	private void OpenLogFile()
	{
		_updateService.OpenLogFile();
	}

	[RelayCommand]
	private void ClearDownloadCache()
	{
		_updateService.ClearDownloadCache();
	}

	[RelayCommand]
	private void Close()
	{
		CloseRequested?.Invoke(this, EventArgs.Empty);
	}
}
