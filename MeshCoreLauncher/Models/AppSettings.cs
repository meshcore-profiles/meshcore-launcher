namespace MeshCoreLauncher.Models;

public class AppSettings
{
	public bool LaunchOnSystemStartup { get; set; }
	public bool StartMinimizedToTray { get; set; } = true;
	public bool AutoLaunchMeshCoreOnStartup { get; set; }
	public bool MinimizeToTrayOnClose { get; set; }
	public bool MinimizeToTrayAfterLaunch { get; set; } = true;
	public bool ExitAfterLaunch { get; set; }
	public bool AskBeforeClosingMeshCore { get; set; }
	public bool NotifyOnUpdateAvailable { get; set; } = true;
	public bool AutoRecheckForUpdatesEnabled { get; set; } = true;
	public int AutoRecheckIntervalMinutes { get; set; } = 120;
	public bool ForceAppImageExtractAndRun { get; set; }
}
