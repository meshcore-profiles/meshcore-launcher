using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MeshCoreLauncher.ViewModels;

namespace MeshCoreLauncher.Converters;

public class StatusKindToBrushConverter : IValueConverter
{
	private static readonly IBrush Idle = new SolidColorBrush(Color.Parse("#5B6478"));
	private static readonly IBrush Checking = new SolidColorBrush(Color.Parse("#22D3EE"));
	private static readonly IBrush UpToDate = new SolidColorBrush(Color.Parse("#34D399"));
	private static readonly IBrush UpdateAvailable = new SolidColorBrush(Color.Parse("#FBBF24"));
	private static readonly IBrush InstallAvailable = new SolidColorBrush(Color.Parse("#F87171"));
	private static readonly IBrush Installing = new SolidColorBrush(Color.Parse("#22D3EE"));
	private static readonly IBrush Error = new SolidColorBrush(Color.Parse("#F87171"));

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value switch
		{
			StatusKind.Idle => Idle,
			StatusKind.Checking => Checking,
			StatusKind.UpToDate => UpToDate,
			StatusKind.UpdateAvailable => UpdateAvailable,
			StatusKind.InstallAvailable => InstallAvailable,
			StatusKind.Installing => Installing,
			StatusKind.Error => Error,
			_ => Idle
		};
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
