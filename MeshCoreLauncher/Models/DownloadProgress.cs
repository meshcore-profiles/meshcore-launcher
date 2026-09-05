namespace MeshCoreLauncher.Models;

public readonly record struct DownloadProgress(long BytesReceived, long TotalBytes, double BytesPerSecond)
{
	public double PercentComplete => TotalBytes > 0 ? BytesReceived * 100.0 / TotalBytes : 0;
}
