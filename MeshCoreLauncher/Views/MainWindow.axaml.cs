using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;

namespace MeshCoreLauncher.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		Opened += (_, _) =>
		{
			RootPanel.Opacity = 1;
			RootPanel.RenderTransform = TransformOperations.Parse("scale(1)");
		};
	}

	private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
	}

	private void Minimize_Click(object? sender, RoutedEventArgs e)
	{
		WindowState = WindowState.Minimized;
	}

	private void Close_Click(object? sender, RoutedEventArgs e)
	{
		Close();
	}
}
