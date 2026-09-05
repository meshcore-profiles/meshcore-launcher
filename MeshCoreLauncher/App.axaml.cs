using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using MeshCoreLauncher.Services;
using MeshCoreLauncher.ViewModels;
using MeshCoreLauncher.Views;

namespace MeshCoreLauncher;

public class App : Application
{
	private bool _isExiting;
	private MainWindow? _mainWindow;
	private TrayIcon? _trayIcon;

	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

			var settingsService = new SettingsService();
			var viewModel = new MainWindowViewModel(settingsService);
			_mainWindow = new MainWindow { DataContext = viewModel };

			viewModel.MinimizeToTrayRequested += (_, _) => _mainWindow.Hide();
			viewModel.OpenSettingsRequested += (_, _) => OpenSettings(settingsService);
			viewModel.ExitRequested += (_, _) => ExitApplication(desktop);
			viewModel.ConfirmationRequested += message => ShowConfirmationAsync(message);

			_mainWindow.Closing += (_, e) =>
			{
				if (_isExiting || !settingsService.Current.MinimizeToTrayOnClose) return;
				e.Cancel = true;
				_mainWindow.Hide();
			};

			SetupTrayIcon(settingsService, viewModel, desktop);

			var startMinimized = desktop.Args?.Contains("--minimized") == true;
			if (startMinimized) _mainWindow.Opened += (_, _) => _mainWindow.Hide();

			desktop.MainWindow = _mainWindow;
		}

		base.OnFrameworkInitializationCompleted();
	}

	private void SetupTrayIcon(SettingsService settingsService, MainWindowViewModel viewModel, IClassicDesktopStyleApplicationLifetime desktop)
	{
		var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://MeshCoreLauncher/Assets/icon.ico")));

		var openItem = new NativeMenuItem("Open MeshCore Launcher");
		openItem.Click += (_, _) => ShowFromTray();

		var checkUpdatesItem = new NativeMenuItem("Check for Updates");
		checkUpdatesItem.Click += (_, _) => viewModel.CheckForUpdatesCommand.Execute(null);

		var settingsItem = new NativeMenuItem("Settings");
		settingsItem.Click += (_, _) => OpenSettings(settingsService);

		var exitItem = new NativeMenuItem("Exit");
		exitItem.Click += (_, _) => ExitApplication(desktop);

		var menu = new NativeMenu { openItem, checkUpdatesItem, settingsItem, new NativeMenuItemSeparator(), exitItem };

		_trayIcon = new TrayIcon { Icon = icon, ToolTipText = "MeshCore Launcher", Menu = menu };
		_trayIcon.Clicked += (_, _) => ShowFromTray();

		TrayIcon.SetIcons(this, [_trayIcon]);

		viewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(MainWindowViewModel.StatusKind) or nameof(MainWindowViewModel.StatusText))
				UpdateTrayTooltip(settingsService, viewModel);
		};
	}

	private void UpdateTrayTooltip(SettingsService settingsService, MainWindowViewModel viewModel)
	{
		if (_trayIcon is null) return;

		_trayIcon.ToolTipText = settingsService.Current.NotifyOnUpdateAvailable
		                        && viewModel.StatusKind is StatusKind.UpdateAvailable or StatusKind.InstallAvailable
			? $"MeshCore Launcher - {viewModel.StatusText}"
			: "MeshCore Launcher";
	}

	private async Task<bool> ShowConfirmationAsync(string message)
	{
		if (_mainWindow is null) return false;

		var dialog = new ConfirmWindow { Message = message };
		return await dialog.ShowDialog<bool>(_mainWindow);
	}

	public void ActivateFromOtherInstance()
	{
		ShowFromTray();
	}

	private void ShowFromTray()
	{
		if (_mainWindow is null) return;
		_mainWindow.Show();
		_mainWindow.WindowState = WindowState.Normal;
		_mainWindow.Activate();
	}

	private void OpenSettings(SettingsService settingsService)
	{
		if (_mainWindow is null) return;

		var vm = new SettingsWindowViewModel(settingsService);
		var window = new SettingsWindow { DataContext = vm };
		vm.CloseRequested += (_, _) => window.Close();

		if (_mainWindow.IsVisible)
			window.ShowDialog(_mainWindow);
		else
			window.Show();
	}

	private void ExitApplication(IClassicDesktopStyleApplicationLifetime desktop)
	{
		_isExiting = true;
		desktop.Shutdown();
	}
}
