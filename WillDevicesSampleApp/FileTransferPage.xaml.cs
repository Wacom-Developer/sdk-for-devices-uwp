using System;
using System.Threading;
using System.Threading.Tasks;
using Wacom.Ink.Model;
using Wacom.Devices;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;
using Wacom.SmartPadCommunication;
using Wacom.UX.ViewModels;
using Windows.Foundation;

namespace WillDevicesSampleApp
{
	public sealed partial class FileTransferPage : Page
	{
		static string s_fileTransferPromptMessage = "Write a note on your Wacom device and press the device's central button to synchronize with the app.";
		CancellationTokenSource m_cts = new CancellationTokenSource();
		int m_retryCounter = 0;

		public FileTransferPage()
		{
			this.InitializeComponent();

			Loaded += FileTransferPage_Loaded;

			NavigationCacheMode = NavigationCacheMode.Disabled;
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += FileTransferPage_BackRequested;
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			Frame rootFrame = Window.Current.Content as Frame;

			if (rootFrame.CanGoBack)
			{
				// Show UI in title bar if opted-in and in-app backstack is not empty.
				SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
			}
			else
			{
				// Remove the UI from the title bar if in-app back stack is empty.
				SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
			}
		}

		protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
		{
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= FileTransferPage_BackRequested;
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			IDigitalInkDevice device = AppObjects.Instance.Device;

			if (device != null)
			{
				device.PairingModeEnabledCallback = null;
				device.DeviceStatusChanged -= OnDeviceStatusChanged;
				device.Disconnected -= OnDeviceDisconnected;
			}

			m_cts.Cancel();
		}

		private async void FileTransferPage_BackRequested(object sender, BackRequestedEventArgs e)
		{
			IFileTransferService service = AppObjects.Instance.Device.GetService(InkDeviceService.FileTransfer) as IFileTransferService;

			if ((service != null) && service.IsStarted)
			{
				await service.StopAsync(m_cts.Token);
			}

			if (Frame.CanGoBack)
			{
				Frame.GoBack();
			}
		}

		private async void FileTransferPage_Loaded(object sender, RoutedEventArgs e)
		{
			IDigitalInkDevice device = AppObjects.Instance.Device;

			device.Disconnected += OnDeviceDisconnected;
			device.DeviceStatusChanged += OnDeviceStatusChanged;
			device.PairingModeEnabledCallback = OnPairingModeEnabledAsync;

			IFileTransferService service = device.GetService(InkDeviceService.FileTransfer) as IFileTransferService;

			if (service == null)
			{
				textBlockPrompt.Text = "The File Transfer service is not supported on this device";
				return;
			}

			try
			{
				textBlockPrompt.Text = s_fileTransferPromptMessage;

				uint width = (uint)await device.GetPropertyAsync("Width", m_cts.Token);
				uint height = (uint)await device.GetPropertyAsync("Height", m_cts.Token);
				uint ptSize = (uint)await device.GetPropertyAsync("PointSize", m_cts.Token);

				service.Transform = AppObjects.CalculateTransform(width, height, ptSize);

				if (!service.IsStarted)
				{
					await service.StartAsync(StrokesReceivedAsync, false, m_cts.Token);
				}
			}
			catch (Exception)
			{
			}
		}

		private async Task<FileTransferControlOptions> StrokesReceivedAsync(InkDocument inkDocument, Exception fileTransferException)
		{
			if (fileTransferException is FileTransferException)
			{
				if (m_retryCounter < 3)
				{
					m_retryCounter++;
					return FileTransferControlOptions.Retry;
				}
			}

			m_retryCounter = 0;

			if (fileTransferException != null)
			{
				return FileTransferControlOptions.Continue;
			}

			// swap document width and height
			if (inkDocument != null)
			{
				Rect bounds = inkDocument.Bounds;
				inkDocument.Bounds = new Rect(0, 0, bounds.Height, bounds.Width);
			}

			await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				InkDocumentDisplayItem item = (inkDocument == null) ?
					new InkDocumentDisplayItem(fileTransferException.Message) :
					new InkDocumentDisplayItem(inkDocument);

				listView.Items.Add(item);

				listView.SelectedIndex = listView.Items.Count - 1;
			});

			return FileTransferControlOptions.Continue;
		}

		private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var item = listView.SelectedItem as InkDocumentDisplayItem;

			if (item != null)
			{
				inkCanvas.InkCanvasDocument = InkCanvasDocument.FromInkDocument(item.Document);
			}
		}

		private void OnDeviceStatusChanged(object sender, DeviceStatusChangedEventArgs e)
		{
			var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
			{
				switch (e.Status)
				{
					case DeviceStatus.ExpectingReconnect:
						textBlockPrompt.Text = s_fileTransferPromptMessage;
						break;

					case DeviceStatus.NotAuthorizedConnectionNotConfirmed:
						await new MessageDialog(AppObjects.GetStringForDeviceStatus(e.Status)).ShowAsync();
						Frame.Navigate(typeof(ScanAndConnectPage));
						break;

					default:
						textBlockPrompt.Text = AppObjects.GetStringForDeviceStatus(e.Status);
						break;
				}
			});
		}

		private async Task<bool> OnPairingModeEnabledAsync(bool authorizedInThisSession)
		{
			if (!authorizedInThisSession)
				return true;

			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

			var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
			{
				bool keepUsingDevice = await AppObjects.Instance.ShowPairingModeEnabledDialogAsync();

				tcs.SetResult(keepUsingDevice);

				if (!keepUsingDevice)
				{
					Frame.Navigate(typeof(ScanAndConnectPage));
				}
			});

			return await tcs.Task;
		}

		private void OnDeviceDisconnected(object sender, EventArgs e)
		{
			AppObjects.Instance.Device = null;

			var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
			{
				await new MessageDialog($"The device {AppObjects.Instance.DeviceInfo.DeviceName} was disconnected.").ShowAsync();

				Frame.Navigate(typeof(ScanAndConnectPage));
			});
		}
	}
}
