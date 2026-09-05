using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MeshCoreLauncher.Services;

public class AutostartService
{
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string RunValueName = "MeshCoreLauncher";
	private const string MinimizedArg = "--minimized";

	public void SetEnabled(bool enabled, bool startMinimized)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			SetEnabledWindows(enabled, startMinimized);
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			SetEnabledMacOs(enabled, startMinimized);
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			SetEnabledLinux(enabled, startMinimized);
	}

	[SupportedOSPlatform("windows")]
	private static void SetEnabledWindows(bool enabled, bool startMinimized)
	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
		if (key is null) return;

		if (!enabled)
		{
			key.DeleteValue(RunValueName, false);
			return;
		}

		var exePath = Environment.ProcessPath;
		if (string.IsNullOrEmpty(exePath)) return;

		var command = $"\"{exePath}\"" + (startMinimized ? $" {MinimizedArg}" : "");
		key.SetValue(RunValueName, command, RegistryValueKind.String);
	}

	private static void SetEnabledMacOs(bool enabled, bool startMinimized)
	{
		var plistPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
			"Library", "LaunchAgents", "net.sefinek.meshcorelauncher.plist");

		if (!enabled)
		{
			if (File.Exists(plistPath)) File.Delete(plistPath);
			return;
		}

		var exePath = Environment.ProcessPath;
		if (string.IsNullOrEmpty(exePath)) return;

		var argsXml = startMinimized ? $"<string>{MinimizedArg}</string>" : "";
		var plist = $"""
		             <?xml version="1.0" encoding="UTF-8"?>
		             <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
		             <plist version="1.0">
		             <dict>
		             	<key>Label</key>
		             	<string>net.sefinek.meshcorelauncher</string>
		             	<key>ProgramArguments</key>
		             	<array>
		             		<string>{exePath}</string>
		             		{argsXml}
		             	</array>
		             	<key>RunAtLoad</key>
		             	<true/>
		             </dict>
		             </plist>
		             """;

		Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
		File.WriteAllText(plistPath, plist);
	}

	private static void SetEnabledLinux(bool enabled, bool startMinimized)
	{
		var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
		var configHome = string.IsNullOrEmpty(xdgConfigHome)
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
			: xdgConfigHome;
		var desktopPath = Path.Combine(configHome, "autostart", "meshcorelauncher.desktop");

		if (!enabled)
		{
			if (File.Exists(desktopPath)) File.Delete(desktopPath);
			return;
		}

		var exePath = Environment.ProcessPath;
		if (string.IsNullOrEmpty(exePath)) return;

		var exec = startMinimized ? $"\"{exePath}\" {MinimizedArg}" : $"\"{exePath}\"";
		var desktopEntry = $"""
		                    [Desktop Entry]
		                    Type=Application
		                    Name=MeshCore Launcher
		                    Exec={exec}
		                    X-GNOME-Autostart-enabled=true
		                    """;

		Directory.CreateDirectory(Path.GetDirectoryName(desktopPath)!);
		File.WriteAllText(desktopPath, desktopEntry);
	}
}
