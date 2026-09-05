using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MeshCoreLauncher.Models;

namespace MeshCoreLauncher.Services;

public class ApiClient
{
	private const string BaseUrl = "https://api.sefinek.net/api/v2";

	public ApiClient()
	{
		Raw = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
		var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
		Raw.DefaultRequestHeaders.UserAgent.ParseAdd($"Mozilla/5.0 (compatible; MeshCoreLauncher/{version}; +https://meshcoreprofiles.com)");
	}

	public HttpClient Raw { get; }

	public async Task<VersionInfo> GetLatestAsync(string platformKey, CancellationToken ct = default)
	{
		VersionInfo info = await Raw.GetFromJsonAsync<VersionInfo>($"{BaseUrl}/meshcore/latest/{platformKey}", ct)
		                   ?? throw new InvalidOperationException("Empty response from the version API.");
		return !info.Success ? throw new InvalidOperationException("Version API reported failure.") : info;
	}
}
