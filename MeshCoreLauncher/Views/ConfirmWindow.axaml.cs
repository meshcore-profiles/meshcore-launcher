using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MeshCoreLauncher.Views;

public partial class ConfirmWindow : Window
{
	public ConfirmWindow()
	{
		InitializeComponent();
	}

	public string Message
	{
		get => MessageText.Text ?? "";
		set => MessageText.Text = value;
	}

	public string ConfirmText
	{
		get => ConfirmButton.Content?.ToString() ?? "";
		set => ConfirmButton.Content = value;
	}

	private void Confirm_Click(object? sender, RoutedEventArgs e)
	{
		Close(true);
	}

	private void Cancel_Click(object? sender, RoutedEventArgs e)
	{
		Close(false);
	}
}
