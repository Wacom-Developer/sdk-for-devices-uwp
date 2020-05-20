using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Wacom;
using Wacom.Devices;
using Wacom.SmartPadCommunication;
using Windows.Devices.Bluetooth;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace WillDevicesSampleApp
{
	public sealed partial class ScanAndConnectPage : Page
	{
		InkDeviceWatcherBLE m_watcherBLE;
		InkDeviceWatcherUSB m_watcherUSB;
		InkDeviceWatcherBTC m_watcherBTC;
		InkDeviceInfo m_connectingDeviceInfo;
		ObservableCollection<InkDeviceInfo> m_deviceInfos = new ObservableCollection<InkDeviceInfo>();


		public ScanAndConnectPage()
		{
			this.InitializeComponent();

			this.DataContext = this;

			m_watcherBLE = new InkDeviceWatcherBLE();
			m_watcherBLE.DeviceAdded += OnDeviceAdded;
			m_watcherBLE.DeviceRemoved += OnDeviceRemoved;
			m_watcherBLE.WatcherStopped += OnBleWatcherStopped;

			m_watcherUSB = new InkDeviceWatcherUSB();
			m_watcherUSB.DeviceAdded += OnDeviceAdded;
			m_watcherUSB.DeviceRemoved += OnDeviceRemoved;
			m_watcherUSB.WatcherStopped += OnUsbWatcherStopped;

			m_watcherBTC = new InkDeviceWatcherBTC();
			m_watcherBTC.DeviceAdded += OnDeviceAdded;
			m_watcherBTC.DeviceRemoved += OnDeviceRemoved;
			m_watcherBTC.WatcherStopped += OnBtcWatcherStopped;

			Loaded += ScanAndConnectPage_Loaded;
			Unloaded += ScanAndConnectPage_Unloaded;

			Application.Current.Suspending += OnAppSuspending;
			Application.Current.Resuming += OnAppResuming;

			SystemNavigationManager.GetForCurrentView().BackRequested += ScanAndConnectPage_BackRequested;
		}

		public ObservableCollection<InkDeviceInfo> DeviceInfos
		{
			get
			{
				return m_deviceInfos;
			}
		}

		private void ScanAndConnectPage_BackRequested(object sender, BackRequestedEventArgs e)
		{
			Frame rootFrame = Window.Current.Content as Frame;
			if (rootFrame == null)
				return;

			// Navigate back if possible, and if the event has not 
			// already been handled .
			if (rootFrame.CanGoBack && e.Handled == false)
			{
				StopScanning();

				e.Handled = true;
				rootFrame.GoBack();
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
		}

		protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
		{
			SystemNavigationManager.GetForCurrentView().BackRequested -= ScanAndConnectPage_BackRequested;
		}

		private void ScanAndConnectPage_Loaded(object sender, RoutedEventArgs e)
		{
			AppObjects.Instance.DeviceInfo = null;

			if (AppObjects.Instance.Device != null)
			{
				AppObjects.Instance.Device.Close();
				AppObjects.Instance.Device = null;
			}

			StartScanning();
		}

		private void ScanAndConnectPage_Unloaded(object sender, RoutedEventArgs e)
		{
			if (AppObjects.Instance.Device != null)
			{
				AppObjects.Instance.Device.DeviceStatusChanged -= OnDeviceStatusChanged;
			}

			StopWatchers();

			Application.Current.Suspending -= OnAppSuspending;
			Application.Current.Resuming -= OnAppResuming;

			m_watcherBLE.DeviceAdded -= OnDeviceAdded;
			m_watcherBLE.DeviceRemoved -= OnDeviceRemoved;
			m_watcherBLE.WatcherStopped -= OnBleWatcherStopped;

			m_watcherUSB.DeviceAdded -= OnDeviceAdded;
			m_watcherUSB.DeviceRemoved -= OnDeviceRemoved;
			m_watcherUSB.WatcherStopped -= OnUsbWatcherStopped;

			m_watcherBTC.DeviceAdded -= OnDeviceAdded;
			m_watcherBTC.DeviceRemoved -= OnDeviceRemoved;
			m_watcherBTC.WatcherStopped -= OnBtcWatcherStopped;
		}

		private void StartScanning()
		{
			StartWatchers();

			SetScanningAndDisabled(btnBleScan);
			SetScanningAndDisabled(btnUsbScan);
			SetScanningAndDisabled(btnBtcScan);

			TextBoxBleSetText();
			TextBoxUsbSetText();
			TextBoxBtcSetText();
		}

		private void StopScanning()
		{
			StopWatchers();

			SetScanAndDisabled(btnBleScan);
			SetScanAndDisabled(btnUsbScan);
			SetScanAndDisabled(btnBtcScan);

			tbBle.Text = string.Empty;
			tbUsb.Text = string.Empty;
			tbBtc.Text = string.Empty;
		}

		private void StartWatchers()
		{
			m_watcherBLE.Start();
			m_watcherUSB.Start();
			m_watcherBTC.Start();
		}

		private void StopWatchers()
		{
			m_watcherBLE.Stop();
			m_watcherUSB.Stop();
			m_watcherBTC.Stop();
		}

		private async void OnButtonConnectClick(object sender, RoutedEventArgs e)
		{
			int index = listView.SelectedIndex;

			if ((index < 0) || (index >= m_deviceInfos.Count))
				return;

			IDigitalInkDevice device = null;
			m_connectingDeviceInfo = m_deviceInfos[index];

			btnConnect.IsEnabled = false;

			StopScanning();

			SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

			if (m_connectingDeviceInfo != null)
			{
				string msg = $"Initializing connection with device: \"{m_connectingDeviceInfo.DeviceName}\"";

				switch (m_connectingDeviceInfo.TransportProtocol)
				{
					case TransportProtocol.BLE:
						tbBle.Text = msg;
						break;

					case TransportProtocol.USB:
						tbUsb.Text = msg;
						break;

					case TransportProtocol.BTC:
						tbBtc.Text = msg;
						break;
				}
			}

			try
			{
				device = await InkDeviceFactory.Instance.CreateDeviceAsync(m_connectingDeviceInfo, AppObjects.Instance.AppId, true, false, OnDeviceStatusChanged);
			}
			catch (Exception ex)
			{
				string message = $"Device creation failed: {ex.Message}";

				await new MessageDialog(message).ShowAsync();
			}

			if (device == null)
			{
				SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
				m_connectingDeviceInfo = null;
				btnConnect.IsEnabled = true;
				StartScanning();
				return;
			}

			AppObjects.Instance.DeviceInfo = m_connectingDeviceInfo;
			AppObjects.Instance.Device = device;
			m_connectingDeviceInfo = null;

			await AppObjects.SerializeDeviceInfoAsync(AppObjects.Instance.DeviceInfo);

			if (Frame.CanGoBack)
			{
				Frame.GoBack();
			}
		}

		private void OnButtonBleScanClick(object sender, RoutedEventArgs e)
		{
			if (m_watcherBLE.Status != Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStatus.Started &&
				m_watcherBLE.Status != Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStatus.Stopping)
			{
				m_watcherBLE.Start();
				SetScanningAndDisabled(btnBleScan);
				TextBoxBleSetText();
			}
		}

		private void OnButtonUsbScanClick(object sender, RoutedEventArgs e)
		{
			if (m_watcherUSB.Status != Windows.Devices.Enumeration.DeviceWatcherStatus.Started &&
				m_watcherUSB.Status != Windows.Devices.Enumeration.DeviceWatcherStatus.Stopping &&
				m_watcherUSB.Status != Windows.Devices.Enumeration.DeviceWatcherStatus.EnumerationCompleted)
			{
				m_watcherUSB.Start();
				SetScanningAndDisabled(btnUsbScan);
				TextBoxUsbSetText();
			}
		}

		private void OnButtonBtcScanClick(object sender, RoutedEventArgs e)
		{
			if (m_watcherBTC.Status != Windows.Devices.Enumeration.DeviceWatcherStatus.Started &&
				m_watcherBTC.Status != Windows.Devices.Enumeration.DeviceWatcherStatus.Stopping &&
				m_watcherBTC.Status != Windows.Devices.Enumeration.DeviceWatcherStatus.EnumerationCompleted)
			{
				m_watcherBTC.Start();
				SetScanningAndDisabled(btnBtcScan);
				TextBoxBtcSetText();
			}
		}

		private void OnDeviceStatusChanged(object sender, DeviceStatusChangedEventArgs e)
		{
			IDigitalInkDevice device = sender as IDigitalInkDevice;

			if (device == null)
				return;

			TextBlock textBlock = null;

			switch (device.TransportProtocol)
			{
				case TransportProtocol.BLE:
					textBlock = tbBle;
					break;

				case TransportProtocol.USB:
					textBlock = tbUsb;
					break;

				case TransportProtocol.BTC:
					textBlock = tbBtc;
					break;
			}

			var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				textBlock.Text = AppObjects.GetStringForDeviceStatus(e.Status);
			});
		}

		private void OnDeviceAdded(object sender, InkDeviceInfo info)
		{
			var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				m_deviceInfos.Add(info);
			});
		}

		private void OnDeviceRemoved(object sender, InkDeviceInfo info)
		{
			var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				RemoveDevice(info);
			});
		}

		private void OnBleWatcherStopped(object sender, Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcherStoppedEventArgs e)
		{
			var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				string message = string.Empty;

				switch (e.Error)
				{
					case BluetoothError.Success:
						if ((m_connectingDeviceInfo != null) && (m_connectingDeviceInfo.TransportProtocol == TransportProtocol.BLE))
						{
							return;
						}

						message = "Not scanning";
						break;

					case BluetoothError.RadioNotAvailable:
						message = "Bluetooth is turned off, please turn it on.";
						SetScanAndEnabled(btnBleScan);
						break;

					case BluetoothError.ResourceInUse:
						message = "Not scanning: Resource In Use";
						break;

					case BluetoothError.DeviceNotConnected:
						message = "Not scanning: Device Not Connected";
						break;

					case BluetoothError.OtherError:
						message = "Not scanning: Unexpected Error";
						break;

					case BluetoothError.DisabledByPolicy:
						message = "Not scanning: Disabled By Policy";
						break;

					case BluetoothError.NotSupported:
						message = "Not scanning: Not Supported";
						break;

					case BluetoothError.DisabledByUser:
						message = "Not scanning: Disabled By User";
						break;

					case BluetoothError.ConsentRequired:
						message = "Not scanning: Consent Required";
						break;

					default:
						throw new Exception("Unknown Bluetooth error!");
				}

				tbBle.Text = message;
			});
		}

		private void OnUsbWatcherStopped(object sender, object e)
		{
			var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				if ((m_connectingDeviceInfo != null) && (m_connectingDeviceInfo.TransportProtocol == TransportProtocol.USB))
					return;

				tbUsb.Text = "Not scanning";
			});
		}

		private void OnBtcWatcherStopped(object sender, object e)
		{
			var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				if ((m_connectingDeviceInfo != null) && (m_connectingDeviceInfo.TransportProtocol == TransportProtocol.BTC))
					return;

				tbBtc.Text = "Not scanning";
			});
		}

		private void OnAppSuspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
		{
			StopWatchers();
		}

		private void OnAppResuming(object sender, object e)
		{
			if (AppObjects.Instance.DeviceInfo == null)
			{
				StartScanning();
			}
		}

		private void RemoveDevice(InkDeviceInfo info)
		{
			int index = -1;

			for (int i = 0; i < m_deviceInfos.Count; i++)
			{
				if (ReferenceEquals(m_deviceInfos[i], info))
				{
					index = i;
					break;
				}
			}

			if (index != -1)
			{
				m_deviceInfos.RemoveAt(index);
			}
		}

		#region Set UI Elements

		private void SetScanningAndDisabled(Button btn)
		{
			btn.Content = "Scanning";
			btn.IsEnabled = false;
		}

		private void SetScanAndEnabled(Button btn)
		{
			btn.Content = "Scan";
			btn.IsEnabled = true;
		}

		private void SetScanAndDisabled(Button btn)
		{
			btn.Content = "Scan";
			btn.IsEnabled = false;
		}

		private void TextBoxBleSetText()
		{
			tbBle.Text =
				"Press and hold the device's Central Button and wait until the blue LED starts to blink. " +
				"When the device appears in the list, select it and press the Connect button.";
		}

		private void TextBoxUsbSetText()
		{
			tbUsb.Text = "Connect the device to a USB port and turn it on.";
		}

		private void TextBoxBtcSetText()
		{
			tbBtc.Text =
				"Press and hold the device's Central Button and wait until the blue LED starts to blink. " +
				"When the device appears in the list, select it and press the Connect button.";
		}

		#endregion
	}
}