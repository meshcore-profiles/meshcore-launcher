using System;
using System.IO;
using System.Text.Json;
using MeshCoreLauncher.Models;

namespace MeshCoreLauncher.Services;

public class SettingsService
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly string _filePath = Path.Combine(PlatformInfo.Current.BaseDir, "settings.json");

	public SettingsService()
	{
		Current = Load();
	}

	public AppSettings Current { get; }

	public void Save()
	{
		Directory.CreateDirectory(PlatformInfo.Current.BaseDir);
		File.WriteAllText(_filePath, JsonSerializer.Serialize(Current, JsonOptions));
	}

	private AppSettings Load()
	{
		if (!File.Exists(_filePath)) return new AppSettings();

		try
		{
			return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath)) ?? new AppSettings();
		}
		catch (Exception)
		{
			return new AppSettings();
		}
	}
}
