using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MeshCoreLauncher.Views;

public partial class SettingsWindow : Window
{
	public SettingsWindow()
	{
		InitializeComponent();
	}

	private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
	}

	private void Close_Click(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
