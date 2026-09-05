using System;
using System.Text.Json.Serialization;

namespace MeshCoreLauncher.Models;

public class VersionInfo
{
	[JsonPropertyName("success")] public bool Success { get; set; }

	[JsonPropertyName("version")] public string Version { get; set; } = string.Empty;

	[JsonPropertyName("build")] public string Build { get; set; } = string.Empty;

	[JsonPropertyName("commit")] public string Commit { get; set; } = string.Empty;

	[JsonPropertyName("releasedAt")] public DateTimeOffset ReleasedAt { get; set; }

	[JsonPropertyName("filename")] public string Filename { get; set; } = string.Empty;

	[JsonPropertyName("url")] public string Url { get; set; } = string.Empty;

	[JsonPropertyName("size")] public long Size { get; set; }

	[JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
}

public class InstalledVersion
{
	public string Version { get; set; } = string.Empty;
	public string Build { get; set; } = string.Empty;
	public string Commit { get; set; } = string.Empty;
	public string Sha256 { get; set; } = string.Empty;
}
