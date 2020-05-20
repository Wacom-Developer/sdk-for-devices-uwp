using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Wacom.Devices;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Radios;
using System.Collections.Generic;

namespace WillDevicesSampleApp
{
	public sealed partial class MainPage : Page
	{
		CancellationTokenSource m_cts = new CancellationTokenSource();
		ObservableCollection<DevicePropertyValuePair> m_propertiesCollection;
		
		public MainPage()
		{
			this.InitializeComponent();

			Loaded += MainPage_Loaded;

			buttonFileTransfer.IsEnabled = false;
			buttonRealTime.IsEnabled = false;
			buttonScan.IsEnabled = false;

			m_propertiesCollection = new ObservableCollection<DevicePropertyValuePair>()
			{
				new DevicePropertyValuePair("Name"),
				new DevicePropertyValuePair("ESN"),
				new DevicePropertyValuePair("Width"),
				new DevicePropertyValuePair("Height"),
				new DevicePropertyValuePair("Point"),
				new DevicePropertyValuePair("Battery")
			};

			gridViewDeviceProperties.ItemsSource = m_propertiesCollection;
		}

		private async void MainPage_Loaded(object sender, RoutedEventArgs e)
		{
			buttonScan.IsEnabled = false;
			buttonFileTransfer.IsEnabled = false;
			buttonRealTime.IsEnabled = false;

			if (AppObjects.Instance.DeviceInfo == null)
			{
				AppObjects.Instance.DeviceInfo = await AppObjects.DeserializeDeviceInfoAsync();
			}

			if (AppObjects.Instance.DeviceInfo == null)
			{
				textBlockDeviceName.Text = "Not connected to a device, click the \"Scan for Devices\" button and follow the instructions.";
				buttonScan.IsEnabled = true;
				return;
			}

			InkDeviceInfo inkDeviceInfo = AppObjects.Instance.DeviceInfo;
			textBlockDeviceName.Text = $"Reconnecting to device {inkDeviceInfo.DeviceName} ({inkDeviceInfo.TransportProtocol}) ...";

			try
			{
				if (AppObjects.Instance.Device == null)
				{
					AppObjects.Instance.Device = await InkDeviceFactory.Instance.CreateDeviceAsync(inkDeviceInfo, AppObjects.Instance.AppId, false, false, OnDeviceStatusChanged);
				}

				AppObjects.Instance.Device.Disconnected += OnDeviceDisconnected;
				AppObjects.Instance.Device.DeviceStatusChanged += OnDeviceStatusChanged;
				AppObjects.Instance.Device.PairingModeEnabledCallback = OnPairingModeEnabledAsync;
			}
			catch (Exception ex)
			{
				textBlockDeviceName.Text = $"Cannot init device: {inkDeviceInfo.DeviceName} [{ex.Message}]";
				buttonScan.IsEnabled = true;
				return;
			}

			textBlockDeviceName.Text = $"Current device: {inkDeviceInfo.DeviceName}";
			buttonFileTransfer.IsEnabled = true;
			buttonRealTime.IsEnabled = true;
			buttonScan.IsEnabled = true;

			textBlockStatus.Text = AppObjects.GetStringForDeviceStatus(AppObjects.Instance.Device.DeviceStatus);

			await DisplayDevicePropertiesAsync();
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
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

		private async Task DisplayDevicePropertiesAsync()
		{
			IDigitalInkDevice device = AppObjects.Instance.Device;

			try
			{
				m_propertiesCollection[0].PropertyValue = (string)await device.GetPropertyAsync(SmartPadProperties.DeviceName, m_cts.Token);
				m_propertiesCollection[1].PropertyValue = (string)await device.GetPropertyAsync(SmartPadProperties.SerialNumber, m_cts.Token);
				m_propertiesCollection[2].PropertyValue = ((uint)await device.GetPropertyAsync(SmartPadProperties.Width, m_cts.Token)).ToString();
				m_propertiesCollection[3].PropertyValue = ((uint)await device.GetPropertyAsync(SmartPadProperties.Height, m_cts.Token)).ToString();
				m_propertiesCollection[4].PropertyValue = ((uint)await device.GetPropertyAsync(SmartPadProperties.PointSize, m_cts.Token)).ToString();
				m_propertiesCollection[5].PropertyValue = ((int)await device.GetPropertyAsync(SmartPadProperties.BatteryLevel, m_cts.Token)).ToString() + "%";
			}
			catch (Exception ex)
			{
				textBlockStatus.Text = $"Exception: {ex.Message}";
				buttonFileTransfer.IsEnabled = false;
				buttonRealTime.IsEnabled = false;
				buttonScan.IsEnabled = true;
			}
		}

		private void ButtonScan_Click(object sender, RoutedEventArgs e)
		{
			Frame.Navigate(typeof(ScanAndConnectPage));
		}

		private void ButtonFileTransfer_Click(object sender, RoutedEventArgs e)
		{
			Frame.Navigate(typeof(FileTransferPage));
		}

		private void ButtonRealTime_Click(object sender, RoutedEventArgs e)
		{
			Frame.Navigate(typeof(RealTimeInkPage));
		}

		private void OnDeviceStatusChanged(object sender, DeviceStatusChangedEventArgs e)
		{
			var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
			{
				switch (e.Status)
				{
					case DeviceStatus.Idle:
						textBlockStatus.Text = AppObjects.GetStringForDeviceStatus(e.Status);
						buttonFileTransfer.IsEnabled = true;
						buttonRealTime.IsEnabled = true;
						break;

					case DeviceStatus.ExpectingConnectionConfirmation:
						textBlockStatus.Text = AppObjects.GetStringForDeviceStatus(e.Status);
						buttonFileTransfer.IsEnabled = false;
						buttonRealTime.IsEnabled = false;
						break;

					case DeviceStatus.NotAuthorizedConnectionNotConfirmed:
						await new MessageDialog(AppObjects.GetStringForDeviceStatus(e.Status)).ShowAsync();
						Frame.Navigate(typeof(ScanAndConnectPage));
						break;

					default:
						textBlockStatus.Text = AppObjects.GetStringForDeviceStatus(e.Status);
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
