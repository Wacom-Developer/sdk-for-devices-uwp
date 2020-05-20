using System;
using System.Diagnostics;
using System.Threading;
using Wacom.Devices;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;
using Wacom.UX.ViewModels;
using Wacom.UX.Gestures;

namespace WillDevicesSampleApp
{
	public sealed partial class RealTimeInkPage : Page
	{
        private const float micrometerToDip = 96.0f / 25400.0f;

        private CancellationTokenSource m_cts = new CancellationTokenSource();

		public RealTimeInkPage()
		{
			this.InitializeComponent();

			Loaded += RealTimeInkPage_Loaded;

			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += RealTimeInkPage_BackRequested;
		}

		private async void RealTimeInkPage_BackRequested(object sender, BackRequestedEventArgs e)
		{
			if (AppObjects.Instance.Device != null)
			{
				IRealTimeInkService service = AppObjects.Instance.Device.GetService(InkDeviceService.RealTimeInk) as IRealTimeInkService;

				if ((service != null) && service.IsStarted)
				{
					await service.StopAsync(m_cts.Token);
				}
			}

			if (Frame.CanGoBack)
			{
				Frame.GoBack();
			}
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
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= RealTimeInkPage_BackRequested;
		}

		private async void RealTimeInkPage_Loaded(object sender, RoutedEventArgs e)
		{
			IDigitalInkDevice device = AppObjects.Instance.Device;

			if (device == null)
			{
				textBlockPrompt.Text = "Device not connected";
				return;
			}

			device.Disconnected += OnDeviceDisconnected;
			device.DeviceStatusChanged += OnDeviceStatusChanged;
			device.PairingModeEnabledCallback = OnPairingModeEnabledAsync;

			IRealTimeInkService service = device.GetService(InkDeviceService.RealTimeInk) as IRealTimeInkService;
			service.NewPage += OnNewPage;
			service.HoverPointReceived += OnHoverPointReceived;
            //service.StrokeStarted += Service_StrokeStarted;
            //service.StrokeEnded += Service_StrokeEnded;
            //service.StrokeUpdated += Service_StrokeUpdated;

			if (service == null)
			{
				textBlockPrompt.Text = "The Real-time Ink service is not supported on this device";
				return;
			}

			textBlockPrompt.Text = AppObjects.GetStringForDeviceStatus(device.DeviceStatus);

			try
			{
				uint width = (uint)await device.GetPropertyAsync("Width", m_cts.Token);
				uint height = (uint)await device.GetPropertyAsync("Height", m_cts.Token);
				uint ptSize = (uint)await device.GetPropertyAsync("PointSize", m_cts.Token);

				service.Transform = AppObjects.CalculateTransform(width, height, ptSize);

				float scaleFactor = ptSize * AppObjects.micrometerToDip;

				InkCanvasDocument document = new InkCanvasDocument();
				document.Size = new Windows.Foundation.Size(height * scaleFactor, width * scaleFactor);
				document.InkCanvasLayers.Add(new InkCanvasLayer());

                inkCanvas.InkCanvasDocument = document;
                inkCanvas.GesturesManager = new GesturesManager();
                inkCanvas.StrokeDataProvider = service;

                if (!service.IsStarted)
				{
					await service.StartAsync(false, m_cts.Token);
				}
			}
			catch (Exception)
			{
			}
		}

        private void Service_StrokeUpdated(object sender, StrokeUpdatedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Service_StrokeEnded(object sender, StrokeEndedEventArgs e)
        {
            Debug.WriteLine("Stroke End");
        }

        private void Service_StrokeStarted(object sender, StrokeStartedEventArgs e)
        {
            Debug.WriteLine("Stroke Start");
        }

        private void OnHoverPointReceived(object sender, HoverPointReceivedEventArgs e)
		{
			string hoverPointCoords = string.Empty;

			switch (e.Phase)
			{
				case Wacom.Ink.InputPhase.Begin:
				case Wacom.Ink.InputPhase.Move:
					hoverPointCoords = string.Format("X:{0:0.0}, Y:{1:0.0}", e.X, e.Y);
					break;
			}

			var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				textBlockHoverCoordinates.Text = hoverPointCoords;
			});
		}

		private void OnNewPage(object sender, EventArgs e)
		{
			var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				//inkCanvas.Clear();
			});
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			IDigitalInkDevice device = AppObjects.Instance.Device;

			if (device != null)
			{
				device.PairingModeEnabledCallback = null;
				device.DeviceStatusChanged -= OnDeviceStatusChanged;
				device.Disconnected -= OnDeviceDisconnected;

				IRealTimeInkService service = device.GetService(InkDeviceService.RealTimeInk) as IRealTimeInkService;

				if (service != null)
				{
					service.NewPage -= OnNewPage;
					service.HoverPointReceived -= OnHoverPointReceived;
				}
			}

			m_cts.Cancel();
		}

		private void OnDeviceStatusChanged(object sender, DeviceStatusChangedEventArgs e)
		{
			var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
			{
				switch (e.Status)
				{
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
