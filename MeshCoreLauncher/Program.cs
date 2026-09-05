using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using MeshCoreLauncher.Services;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace MeshCoreLauncher;

internal static class Program
{
	private const string MutexName = @"Global\MeshCoreLauncher-SingleInstance";
	private const string ActivatePipeName = "MeshCoreLauncher-Activate";
	private static readonly Logger Log = LogManager.GetCurrentClassLogger();

	[STAThread]
	public static void Main(string[] args)
	{
		using var instanceMutex = new Mutex(true, MutexName, out var createdNew);
		if (!createdNew)
		{
			NotifyExistingInstance();
			return;
		}

		ConfigureLogging();
		AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			Log.Error(e.Exception, "Unobserved task exception");
			e.SetObserved();
		};

		StartActivationListener();

		try
		{
			BuildAvaloniaApp()
				.StartWithClassicDesktopLifetime(args);
		}
		finally
		{
			LogManager.Shutdown();
		}
	}

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace();
	}

	private static void NotifyExistingInstance()
	{
		try
		{
			using var client = new NamedPipeClientStream(".", ActivatePipeName, PipeDirection.Out);
			client.Connect(1000);
			client.WriteByte(1);
		}
		catch (Exception ex) when (ex is IOException or TimeoutException)
		{
		}
	}

	private static void StartActivationListener()
	{
		var thread = new Thread(() =>
		{
			while (true)
			{
				try
				{
					using var server = new NamedPipeServerStream(ActivatePipeName, PipeDirection.In);
					server.WaitForConnection();
					server.ReadByte();
					Dispatcher.UIThread.Post(() => (Application.Current as App)?.ActivateFromOtherInstance());
				}
				catch (IOException)
				{
					Thread.Sleep(1000);
				}
			}
		})
		{
			IsBackground = true
		};
		thread.Start();
	}

	private static void ConfigureLogging()
	{
		Directory.CreateDirectory(PlatformInfo.Current.BaseDir);
		var logsDir = Path.Combine(PlatformInfo.Current.BaseDir, "logs");

		var config = new LoggingConfiguration();
		var fileTarget = new FileTarget("logfile")
		{
			FileName = Path.Combine(logsDir, "launcher.log"),
			ArchiveFileName = Path.Combine(logsDir, "launcher.{#}.log"),
			ArchiveAboveSize = 5 * 1024 * 1024,
			MaxArchiveFiles = 5,
			Layout = "${longdate} ${level:uppercase=true} ${logger} - ${message} ${exception:format=tostring}"
		};

		config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);
		LogManager.Configuration = config;
	}
}
