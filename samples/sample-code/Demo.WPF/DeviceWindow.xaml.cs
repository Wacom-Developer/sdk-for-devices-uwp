using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Collections.ObjectModel;
using Microsoft.Win32;

namespace Demo.WPF
{
	/// <summary>
	/// Interaction logic for DeviceWindow.xaml
	/// </summary>
	public partial class DeviceWindow : Window, INotifyPropertyChanged
	{
		private readonly Wacom.Devices.IInkDeviceInfo _inkDeviceInfo;
		private readonly SynchronizationContext _synchronizationContext;
		private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();

		private Wacom.Devices.IDigitalInkDevice _digitalInkDevice;
		private Wacom.Devices.IDesktopDisplayService _desktopDisplayService;
		private Wacom.Devices.IDiscreteDisplayService _discreteDisplayService;
		private Wacom.Devices.IRealTimeInkService _realTimeInkService;
		private Wacom.Devices.IEncryptionService _encryptionService;
		private Wacom.Devices.IFileTransferService _fileTransferService;
		//private Wacom.Devices.IPairingModeService _pairingModeService;

		private Wacom.Devices.IInkDeviceNotification<Wacom.Devices.BatteryStateChangedEventArgs> _batteryStatedChangedNotification;

		public DeviceWindow(Wacom.Devices.IInkDeviceInfo inkDeviceInfo)
		{
			_inkDeviceInfo = inkDeviceInfo;
			_synchronizationContext = SynchronizationContext.Current;

			InitializeComponent();
			Initialize_DeviceProperties();

			Title = Title + " - " + inkDeviceInfo.DeviceName;

			tabProperties.Visibility = Visibility.Collapsed;
			tabDesktopDisplay.Visibility = Visibility.Collapsed;
			tabDiscreteDisplay.Visibility = Visibility.Collapsed;
			tabRealTimeInk.Visibility = Visibility.Collapsed;
			tabEncryption.Visibility = Visibility.Collapsed;
			tabFileTransfer.Visibility = Visibility.Collapsed;

			Loaded += (s, e) => Task.Run(async () => await ConnectAsync().ConfigureAwait(continueOnCapturedContext: false), _cancellationToken.Token);
			Unloaded += (s, e) => Disconnect();
			Closed += (s, e) => Disconnect();
		}

		private async Task ConnectAsync()
		{
			try
			{
				DeviceStatus = _inkDeviceInfo.TransportProtocol == Wacom.Devices.TransportProtocol.BLE ? "Loading (you may need to press the device's centre button)..." : "Loading...";
				_digitalInkDevice = await Wacom.Devices.InkDeviceFactory.Instance.CreateDeviceAsync(_inkDeviceInfo, App.AppId, true, false, OnDeviceStatusChanged, PairingModeEnabledCallback).ConfigureAwait(continueOnCapturedContext: false);
				DeviceStatus = "Connected";

				_synchronizationContext.Post(o => tabProperties.Visibility = Visibility.Visible, null);

				GetNotifications();
				await GetServicesAsync().ConfigureAwait(continueOnCapturedContext: false);
				await QueryDevicePropertiesAsync().ConfigureAwait(continueOnCapturedContext: false);

				async Task GetServicesAsync()
				{
					try
					{
						_desktopDisplayService = _digitalInkDevice.GetService(Wacom.Devices.InkDeviceService.DesktopDisplay) as Wacom.Devices.IDesktopDisplayService;
						if (_desktopDisplayService != null)
						{
							_synchronizationContext.Post(o => tabDesktopDisplay.Visibility = Visibility.Visible, null);
							await Initialize_DesktopDisplayAsync().ConfigureAwait(continueOnCapturedContext: false);
						}
					}
					catch (Wacom.Devices.LicensingException)
					{
					}

					try
					{
						_discreteDisplayService = _digitalInkDevice.GetService(Wacom.Devices.InkDeviceService.DiscreteDisplay) as Wacom.Devices.IDiscreteDisplayService;
						if (_discreteDisplayService != null)
						{
							_synchronizationContext.Post(o => tabDiscreteDisplay.Visibility = Visibility.Visible, null);
							await Initialize_DiscreteDisplayAsync().ConfigureAwait(continueOnCapturedContext: false);
						}
					}
					catch (Wacom.Devices.LicensingException)
					{
					}

					try
					{
						_realTimeInkService = _digitalInkDevice.GetService(Wacom.Devices.InkDeviceService.RealTimeInk) as Wacom.Devices.IRealTimeInkService;
						if (_realTimeInkService != null)
						{
							_synchronizationContext.Post(o => tabRealTimeInk.Visibility = Visibility.Visible, null);
							Initialize_RealTimeInk();
						}
					}
					catch (Wacom.Devices.LicensingException)
					{
					}

					try
					{
						_encryptionService = _digitalInkDevice.GetService(Wacom.Devices.InkDeviceService.Encryption) as Wacom.Devices.IEncryptionService;
						if (_encryptionService != null)
						{
							_synchronizationContext.Post(o => tabEncryption.Visibility = Visibility.Visible, null);
							Initialize_Encryption();
						}
					}
					catch (Wacom.Devices.LicensingException)
					{
					}

					try
					{
						_fileTransferService = _digitalInkDevice.GetService(Wacom.Devices.InkDeviceService.FileTransfer) as Wacom.Devices.IFileTransferService;
						if (_fileTransferService != null)
						{
							_synchronizationContext.Post(o => tabFileTransfer.Visibility = Visibility.Visible, null);
							Initialize_FileTransfer();
						}
					}
					catch (Wacom.Devices.LicensingException)
					{
					}

				}

				void GetNotifications()
				{
					_batteryStatedChangedNotification = _digitalInkDevice.GetNotification<Wacom.Devices.BatteryStateChangedEventArgs>(Wacom.Devices.Notification.Device.BatteryStateChanged);
					if (_batteryStatedChangedNotification != null)
					{
						_batteryStatedChangedNotification.Notification += OnBatteryStatedChanged_Notification;
					}
				}

			}
			catch (Exception ex)
			{
				DeviceStatus = $"Connect failed: {ex.Message}";
			}
		}

		private async Task<bool> PairingModeEnabledCallback(bool isAuthorized)
		{
			return await Task.Run(() =>
			{
				SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
				var result = MessageBox.Show(messageBoxText: $"PairingModeEnabled.isAuthorized={isAuthorized}. Return true=yes, false=no", caption: "Pairing Mode", button: MessageBoxButton.YesNo);
				return result == MessageBoxResult.Yes;
			}, _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
		}

		private void Disconnect()
		{
			_cancellationToken.Cancel();

			if (_batteryStatedChangedNotification != null)
			{
				_batteryStatedChangedNotification.Notification -= OnBatteryStatedChanged_Notification;
				_batteryStatedChangedNotification = null;
			}

			if (_digitalInkDevice != null)
			{
				try
				{
					_digitalInkDevice.Close();
				}
				finally
				{
					_digitalInkDevice.Dispose();
					_digitalInkDevice = null;
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChangedSync(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void OnPropertyChanged(string propertyName)
		{
			_synchronizationContext.Post(o => OnPropertyChangedSync(propertyName), null);
		}

		#region Notification Handling
		private void OnBatteryStatedChanged_Notification(object sender, Wacom.Devices.BatteryStateChangedEventArgs e)
		{
		}
		#endregion

		#region DeviceWindow (Status)

		private string _deviceStatus;
		public string DeviceStatus
		{
			get => _deviceStatus;
			private set
			{
				_deviceStatus = value;
				OnPropertyChanged(nameof(DeviceStatus));
			}
		}

		private void OnDeviceStatusChanged(object sender, Wacom.Devices.DeviceStatusChangedEventArgs e)
		{
			DeviceStatus = e.Status.ToString();
		}
		#endregion

		#region DeviceInfo Tab
		public string DeviceInfo_Id => _inkDeviceInfo.Id;
		public string DeviceInfo_TransportProtocol => _inkDeviceInfo.TransportProtocol.ToString();
		public string DeviceInfo_DeviceModel => _inkDeviceInfo.DeviceModel.ToString();
		public string DeviceInfo_DeviceName => _inkDeviceInfo.DeviceName;
		public string DeviceInfo_Stream
		{
			get
			{
				using var stream = new MemoryStream();
				_inkDeviceInfo.ToStream(stream);
				var data = stream.ToArray();
				return System.Text.Encoding.UTF8.GetString(data);
			}
		}

		private void DeviceInfo_Stream_Click(object sender, RoutedEventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Title = "Save Stream",
				AddExtension = true,
				DefaultExt = "stream",
				Filter = "Streams|*.stream|All|*.*",
				OverwritePrompt = true
			};

			if (saveFileDialog.ShowDialog() == true)
			{
				using var stream = new FileStream(saveFileDialog.FileName, FileMode.Create);
				_inkDeviceInfo.ToStream(stream);
			}
		}
		#endregion

		#region DeviceProperties Tab

		private ObservableCollection<DevicePropertyValue> _deviceProperties = new ObservableCollection<DevicePropertyValue>();

		public ObservableCollection<DevicePropertyValue> DeviceWindow_DeviceProperties => _deviceProperties;

		private void Initialize_DeviceProperties()
		{
			foreach (var i in DeviceProperties.Device) _deviceProperties.Add(new DevicePropertyValue(i));
			foreach (var i in DeviceProperties.Digitizer) _deviceProperties.Add(new DevicePropertyValue(i));
			foreach (var i in DeviceProperties.Screen) _deviceProperties.Add(new DevicePropertyValue(i));
			foreach (var i in DeviceProperties.STU) _deviceProperties.Add(new DevicePropertyValue(i));
			foreach (var i in DeviceProperties.SmartPad) _deviceProperties.Add(new DevicePropertyValue(i));
			foreach (var i in DeviceProperties.WacomDriver) _deviceProperties.Add(new DevicePropertyValue(i));
		}


		private async Task QueryDevicePropertiesAsync()
		{
			foreach (var i in _deviceProperties)
			{
				_cancellationToken.Token.ThrowIfCancellationRequested();
				try
				{
					i.SetValue(await _digitalInkDevice.GetPropertyAsync(i.Name, _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false));
				}
				catch
				{
				}
			}
		}

		#endregion

		#region DesktopDisplay Tab
		private System.Drawing.Rectangle _desktopDisplay_MappedPixels;
		private bool _desktopDisplay_TrackCursor;
		private async Task Initialize_DesktopDisplayAsync()
		{
			if (_desktopDisplayService != null)
			{
				OnPropertyChanged(nameof(DesktopDisplay_IsIntegrated));
				OnPropertyChanged(nameof(DesktopDisplay_Id));
				OnPropertyChanged(nameof(DesktopDisplay_ScreenPixels));
				_desktopDisplay_MappedPixels = await _desktopDisplayService.GetMappedPixelsAsync(_cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
				OnPropertyChanged(nameof(DesktopDisplay_MappedPixels));
				_desktopDisplay_TrackCursor = await _desktopDisplayService.GetTrackCursorAsync(_cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
				OnPropertyChanged(nameof(DesktopDisplay_TrackCursor));
			}
		}

		public string DesktopDisplay_IsIntegrated => _desktopDisplayService != null ? (_desktopDisplayService.IsIntegrated ? "YES" : "NO") : "Unknown";
		public string DesktopDisplay_Id => _desktopDisplayService?.Id;
		public string DesktopDisplay_ScreenPixels => _desktopDisplayService != null ? _desktopDisplayService.ScreenPixels.ToString() : "N/A";
		public string DesktopDisplay_MappedPixels => _desktopDisplayService != null ? _desktopDisplay_MappedPixels.ToString() : "N/A";

		public bool DesktopDisplay_TrackCursor
		{
			get => _desktopDisplay_TrackCursor;
			set => Task.Run(async () =>
			{
				if (_desktopDisplayService != null)
				{
					await _desktopDisplayService.SetTrackCursorAsync(value, _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
					_desktopDisplay_TrackCursor = value;
					OnPropertyChanged(nameof(DesktopDisplay_TrackCursor));
				}
			}, _cancellationToken.Token);
		}
		#endregion

		#region DiscreteDisplay Tab

		private System.Drawing.Color _discreteDisplay_InkColor;
		private System.Drawing.Color _discreteDisplay_ClearColor;
		private System.Drawing.Rectangle[] _discreteDisplay_area = new System.Drawing.Rectangle[3]; // 0=Inking, 1=Clear, 2=Draw
		private bool[][] _discreteDisplay_selectArea = new bool[3][] { new bool[] { true, false }, new bool[] { true, false }, new bool[] { true, false } };
		
		private System.Drawing.Rectangle _discreteDisplay_imageArea;
		private System.Drawing.Bitmap _discreteDisplay_image = App.DiscreteDisplaySampleImage();


		private async Task Initialize_DiscreteDisplayAsync()
		{
			if (_discreteDisplayService != null)
			{
				OnPropertyChanged(nameof(DiscreteDisplay_WidthAndHeight));
				OnPropertyChanged(nameof(DiscreteDisplay_PixelFormat));
				OnPropertyChanged(nameof(DiscreteDisplay_SupportsInking));
				OnPropertyChanged(nameof(DiscreteDisplay_SupportsAreaUpdate));
				OnPropertyChanged(nameof(DiscreteDisplay_IsInking));

				if (_discreteDisplayService.PixelFormat != System.Drawing.Imaging.PixelFormat.Format1bppIndexed)
				{
					_discreteDisplay_InkColor = await _discreteDisplayService.GetInkColorAsync(_cancellationToken.Token);
					OnPropertyChanged(nameof(DiscreteDisplay_InkColor_Red));
					OnPropertyChanged(nameof(DiscreteDisplay_InkColor_Green));
					OnPropertyChanged(nameof(DiscreteDisplay_InkColor_Blue));

					_discreteDisplay_ClearColor = System.Drawing.Color.White;
					OnPropertyChanged(nameof(DiscreteDisplay_ClearColor_Red));
					OnPropertyChanged(nameof(DiscreteDisplay_ClearColor_Green));
					OnPropertyChanged(nameof(DiscreteDisplay_ClearColor_Blue));
				}
				else
				{
					_synchronizationContext.Post(o => { spInkColor.IsEnabled = false; spBackgroundColor.IsEnabled = false; }, null);
				}

				if (_discreteDisplayService.SupportsAreaUpdate)
				{
					_discreteDisplay_area[0] = _discreteDisplay_area[1] = _discreteDisplay_area[2] = new System.Drawing.Rectangle(0, 0, (int)_discreteDisplayService.WidthPixels, (int)_discreteDisplayService.HeightPixels);
					OnPropertyChanged(nameof(DiscreteDisplay_Area_X_Inking));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_Y_Inking));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_W_Inking));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_H_Inking));

					OnPropertyChanged(nameof(DiscreteDisplay_Area_X_Clear));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_Y_Clear));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_W_Clear));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_H_Clear));

					OnPropertyChanged(nameof(DiscreteDisplay_Area_X_Draw));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_Y_Draw));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_W_Draw));
					OnPropertyChanged(nameof(DiscreteDisplay_Area_H_Draw));
				}
				else
				{
					_synchronizationContext.Post(o => spDiscreteDisplay_Area_Inking.IsEnabled = spDiscreteDisplay_Area_Clear.IsEnabled = spDiscreteDisplay_Area_Draw.IsEnabled = false, null);
				}

				_discreteDisplay_imageArea = new System.Drawing.Rectangle(0, 0, _discreteDisplay_image.Width, _discreteDisplay_image.Height);
				OnPropertyChanged(nameof(DiscreteDisplay_ImageArea_X));
				OnPropertyChanged(nameof(DiscreteDisplay_ImageArea_Y));
				OnPropertyChanged(nameof(DiscreteDisplay_ImageArea_W));
				OnPropertyChanged(nameof(DiscreteDisplay_ImageArea_H));
			}
		}

		public string DiscreteDisplay_WidthAndHeight => _discreteDisplayService != null ? $"{_discreteDisplayService.WidthPixels} x {_discreteDisplayService.HeightPixels}" : "N/A";

		public string DiscreteDisplay_PixelFormat => _discreteDisplayService != null ? _discreteDisplayService.PixelFormat.ToString() : "N/A";

		public string DiscreteDisplay_SupportsInking => _discreteDisplayService != null ? (_discreteDisplayService.SupportsInking ? "YES" : "NO") : "N/A";

		public string DiscreteDisplay_SupportsAreaUpdate => _discreteDisplayService != null ? (_discreteDisplayService.SupportsAreaUpdate ? "SUPPORTED" : "NOT SUPPORTED") : "N/A";

		public string DiscreteDisplay_IsInking => _discreteDisplayService != null ? (_discreteDisplayService.IsInking ? "YES" : "NO") : "N/A";

		public int DiscreteDisplay_InkColor_Red
		{
			get => _discreteDisplay_InkColor.R;
			set
			{
				_discreteDisplay_InkColor = System.Drawing.Color.FromArgb(value, _discreteDisplay_InkColor.G, _discreteDisplay_InkColor.B);
				OnPropertyChanged(nameof(DiscreteDisplay_InkColor_Red));
				Task.Run(async () => await _discreteDisplayService.SetInkColorAsync(_discreteDisplay_InkColor, _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false), _cancellationToken.Token);
			}
		}

		public int DiscreteDisplay_InkColor_Green
		{
			get => _discreteDisplay_InkColor.G;
			set
			{
				_discreteDisplay_InkColor = System.Drawing.Color.FromArgb(_discreteDisplay_InkColor.R, value, _discreteDisplay_InkColor.B);
				OnPropertyChanged(nameof(DiscreteDisplay_InkColor_Green));
				Task.Run(async () => await _discreteDisplayService.SetInkColorAsync(_discreteDisplay_InkColor, _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false), _cancellationToken.Token);
			}
		}

		public int DiscreteDisplay_InkColor_Blue
		{
			get => _discreteDisplay_InkColor.B;
			set
			{
				_discreteDisplay_InkColor = System.Drawing.Color.FromArgb(_discreteDisplay_InkColor.R, _discreteDisplay_InkColor.G, value);
				OnPropertyChanged(nameof(DiscreteDisplay_InkColor_Blue));
				Task.Run(async () => await _discreteDisplayService.SetInkColorAsync(_discreteDisplay_InkColor, _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false), _cancellationToken.Token);
			}
		}

		public int DiscreteDisplay_ClearColor_Red
		{
			get => _discreteDisplay_ClearColor.R;
			set => _discreteDisplay_ClearColor = System.Drawing.Color.FromArgb(value, _discreteDisplay_ClearColor.G, _discreteDisplay_ClearColor.B);
		}

		public int DiscreteDisplay_ClearColor_Green
		{
			get => _discreteDisplay_ClearColor.G;
			set => _discreteDisplay_ClearColor = System.Drawing.Color.FromArgb(_discreteDisplay_ClearColor.R, value, _discreteDisplay_ClearColor.B);
		}
		public int DiscreteDisplay_ClearColor_Blue
		{
			get => _discreteDisplay_ClearColor.B;
			set => _discreteDisplay_ClearColor = System.Drawing.Color.FromArgb(_discreteDisplay_ClearColor.R, _discreteDisplay_ClearColor.G, value);
		}


		private System.Drawing.Rectangle DiscreteDisplay_GetArea(int index)
		{
			if (_discreteDisplay_selectArea[index][0])
			{
				return default;
			}
			else
			{
				return _discreteDisplay_area[index];
			}
		}

		public bool DiscreteDisplay_Inking
		{
			get => _discreteDisplayService?.IsInking ?? false;
			set
			{
				if (value)
				{
					Task.Run(async () =>
					{
						await _discreteDisplayService.EnableInkingAsync(DiscreteDisplay_GetArea(0), _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
						OnPropertyChanged(nameof(DiscreteDisplay_IsInking));
						OnPropertyChanged(nameof(DiscreteDisplay_Inking));
					}, _cancellationToken.Token);
				}
				else
				{
					Task.Run(async () =>
					{
						await _discreteDisplayService.DisableInkingAsync(_cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
						OnPropertyChanged(nameof(DiscreteDisplay_IsInking));
						OnPropertyChanged(nameof(DiscreteDisplay_Inking));
					}, _cancellationToken.Token);
				}
			}
		}

		private void DiscreteDisplay_Clear_Click(object sender, RoutedEventArgs e)
		{
			Task.Run(async () => await _discreteDisplayService.ClearScreenAsync(_discreteDisplay_ClearColor, DiscreteDisplay_GetArea(1), _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false), _cancellationToken.Token);
		}

		public string DiscreteDisplay_ImageSize => _discreteDisplay_image != null ? $"{_discreteDisplay_image.Width} x {_discreteDisplay_image.Height}" : "";


		public bool[] DiscreteDisplay_Inking_Area_Inking => _discreteDisplay_selectArea[0];
		public bool[] DiscreteDisplay_Inking_Area_Clear => _discreteDisplay_selectArea[1];
		public bool[] DiscreteDisplay_Inking_Area_Draw => _discreteDisplay_selectArea[2];

		private void DiscreteDisplay_set_X(int value, int width, ref System.Drawing.Rectangle area, string nameofX, string nameofW)
		{
			bool adjusted = false;
			if (value < 0)
			{
				value = 0;
				adjusted = true;
			}
			if (value > width - 1)
			{
				value = width - 1;
				adjusted = true;
			}
			area.X = value;
			if (adjusted)
			{
				OnPropertyChanged(nameofX);
			}
			if (area.Right >= width)
			{
				area.Width = width - area.X;
				OnPropertyChanged(nameofW);
			}
		}

		private void DiscreteDisplay_set_Y(int value, int height, ref System.Drawing.Rectangle area, string nameofY, string nameofH)
		{
			bool adjusted = false;
			if (value < 0)
			{
				value = 0;
				adjusted = true;
			}
			if (value > height - 1)
			{
				value = height - 1;
				adjusted = true;
			}
			area.Y = value;
			if (adjusted)
			{
				OnPropertyChanged(nameofY);
			}
			if (area.Bottom >= height)
			{
				area.Height = height - area.Y;
				OnPropertyChanged(nameofH);
			}
		}

		private void DiscreteDisplay_set_W(int value, int width, ref System.Drawing.Rectangle area, string nameofW)
		{
			bool adjusted = false;
			if (value < 1)
			{
				value = 1;
				adjusted = true;
			}
			if (area.X + value > width)
			{
				value = (int)width - area.X;
				adjusted = true;
			}
			area.Width = value;
			if (adjusted)
			{
				OnPropertyChanged(nameofW);
			}
		}

		private void DiscreteDisplay_set_H(int value, int height, ref System.Drawing.Rectangle area, string nameofH)
		{
			bool adjusted = false;
			if (value < 1)
			{
				value = 1;
				adjusted = true;
			}
			if (area.Y + value > height)
			{
				value = (int)height - area.Y;
				adjusted = true;
			}
			area.Height = value;
			if (adjusted)
			{
				OnPropertyChanged(nameofH);
			}
		}

		public int DiscreteDisplay_Area_X_Inking
		{
			get => _discreteDisplay_area[0].X;
			set => DiscreteDisplay_set_X(value, (int)_discreteDisplayService.WidthPixels, ref _discreteDisplay_area[0], nameof(DiscreteDisplay_Area_X_Inking), nameof(DiscreteDisplay_Area_W_Inking));
		}
		public int DiscreteDisplay_Area_Y_Inking
		{
			get => _discreteDisplay_area[0].Y;
			set => DiscreteDisplay_set_Y(value, (int)_discreteDisplayService.HeightPixels, ref _discreteDisplay_area[0], nameof(DiscreteDisplay_Area_Y_Inking), nameof(DiscreteDisplay_Area_H_Inking));
		}
		public int DiscreteDisplay_Area_W_Inking
		{
			get => _discreteDisplay_area[0].Width;
			set => DiscreteDisplay_set_W(value, (int)_discreteDisplayService.WidthPixels, ref _discreteDisplay_area[0], nameof(DiscreteDisplay_Area_W_Inking));
		}
		public int DiscreteDisplay_Area_H_Inking
		{
			get => _discreteDisplay_area[0].Height;
			set => DiscreteDisplay_set_H(value, (int)_discreteDisplayService.HeightPixels, ref _discreteDisplay_area[0], nameof(DiscreteDisplay_Area_H_Inking));
		}

		public int DiscreteDisplay_Area_X_Clear
		{
			get => _discreteDisplay_area[1].X;
			set => DiscreteDisplay_set_X(value, (int)_discreteDisplayService.WidthPixels, ref _discreteDisplay_area[1], nameof(DiscreteDisplay_Area_X_Clear), nameof(DiscreteDisplay_Area_W_Clear));
		}
		public int DiscreteDisplay_Area_Y_Clear
		{
			get => _discreteDisplay_area[1].Y;
			set => DiscreteDisplay_set_Y(value, (int)_discreteDisplayService.HeightPixels, ref _discreteDisplay_area[1], nameof(DiscreteDisplay_Area_Y_Clear), nameof(DiscreteDisplay_Area_H_Clear));
		}
		public int DiscreteDisplay_Area_W_Clear
		{
			get => _discreteDisplay_area[1].Width;
			set => DiscreteDisplay_set_W(value, (int)_discreteDisplayService.WidthPixels, ref _discreteDisplay_area[1], nameof(DiscreteDisplay_Area_W_Clear));
		}
		public int DiscreteDisplay_Area_H_Clear
		{
			get => _discreteDisplay_area[1].Height;
			set => DiscreteDisplay_set_H(value, (int)_discreteDisplayService.HeightPixels, ref _discreteDisplay_area[1], nameof(DiscreteDisplay_Area_H_Clear));
		}

		public int DiscreteDisplay_Area_X_Draw
		{
			get => _discreteDisplay_area[2].X;
			set => DiscreteDisplay_set_X(value, (int)_discreteDisplayService.WidthPixels, ref _discreteDisplay_area[2], nameof(DiscreteDisplay_Area_X_Draw), nameof(DiscreteDisplay_Area_W_Draw));
		}
		public int DiscreteDisplay_Area_Y_Draw
		{
			get => _discreteDisplay_area[2].Y;
			set => DiscreteDisplay_set_Y(value, (int)_discreteDisplayService.HeightPixels, ref _discreteDisplay_area[2], nameof(DiscreteDisplay_Area_Y_Draw), nameof(DiscreteDisplay_Area_H_Draw));
		}
		public int DiscreteDisplay_Area_W_Draw
		{
			get => _discreteDisplay_area[2].Width;
			set => DiscreteDisplay_set_W(value, (int)_discreteDisplayService.WidthPixels, ref _discreteDisplay_area[2], nameof(DiscreteDisplay_Area_W_Draw));
		}
		public int DiscreteDisplay_Area_H_Draw
		{
			get => _discreteDisplay_area[2].Height;
			set => DiscreteDisplay_set_H(value, (int)_discreteDisplayService.HeightPixels, ref _discreteDisplay_area[2], nameof(DiscreteDisplay_Area_H_Draw));
		}

		private void DiscreteDisplay_DrawImage_Click(object sender, RoutedEventArgs e)
		{
			Task.Run(async () => await _discreteDisplayService.DrawImage(_discreteDisplay_image, _discreteDisplay_imageArea, DiscreteDisplay_GetArea(2), _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false), _cancellationToken.Token);
		}

		private void DiscreteDisplay_DrawImage_Open(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "Open Image",

				CheckFileExists = true
			};

			if (openFileDialog.ShowDialog() == true)
			{
				try
				{
					var bitmap = new System.Drawing.Bitmap(openFileDialog.FileName);
					_discreteDisplay_image = bitmap;
					_discreteDisplay_imageArea = new System.Drawing.Rectangle(0, 0, _discreteDisplay_image.Width, _discreteDisplay_image.Height);
					OnPropertyChanged(nameof(DiscreteDisplay_ImageArea_X));
					OnPropertyChanged(nameof(DiscreteDisplay_ImageArea_Y));
					OnPropertyChanged(nameof(DiscreteDisplay_ImageArea_W));
					OnPropertyChanged(nameof(DiscreteDisplay_ImageArea_H));
				}
				catch (Exception ex)
				{
					_synchronizationContext.Post(o => MessageBox.Show($"Unable to load image: {ex.Message}"), null);
				}

			}
		}

		public int DiscreteDisplay_ImageArea_X
		{
			get => _discreteDisplay_imageArea.X;
			set => DiscreteDisplay_set_X(value, _discreteDisplay_image.Width, ref _discreteDisplay_imageArea, nameof(DiscreteDisplay_ImageArea_X), nameof(DiscreteDisplay_ImageArea_W));
		}
		public int DiscreteDisplay_ImageArea_Y
		{
			get => _discreteDisplay_imageArea.Y;
			set => DiscreteDisplay_set_Y(value, _discreteDisplay_image.Height, ref _discreteDisplay_imageArea, nameof(DiscreteDisplay_ImageArea_Y), nameof(DiscreteDisplay_ImageArea_H));
		}
		public int DiscreteDisplay_ImageArea_W
		{
			get => _discreteDisplay_imageArea.Width;
			set => DiscreteDisplay_set_W(value, _discreteDisplay_image.Width, ref _discreteDisplay_imageArea, nameof(DiscreteDisplay_ImageArea_W));
		}
		public int DiscreteDisplay_ImageArea_H
		{
			get => _discreteDisplay_imageArea.Height;
			set => DiscreteDisplay_set_H(value, _discreteDisplay_image.Width, ref _discreteDisplay_imageArea, nameof(DiscreteDisplay_ImageArea_Y));
		}

		#endregion

		#region RealTimeInk Tab

		private void Initialize_RealTimeInk()
		{
			_synchronizationContext.Post(RealTimeInk_InitializeSync, null);

			void RealTimeInk_InitializeSync(object _)
			{
				OnPropertyChangedSync(nameof(RealTimeInk_IsStarted));
				OnPropertyChangedSync(nameof(RealTimeInk_Transform_M11));
				OnPropertyChangedSync(nameof(RealTimeInk_Transform_M12));
				OnPropertyChangedSync(nameof(RealTimeInk_Transform_M21));
				OnPropertyChangedSync(nameof(RealTimeInk_Transform_M22));
				OnPropertyChangedSync(nameof(RealTimeInk_Transform_M31));
				OnPropertyChangedSync(nameof(RealTimeInk_Transform_M32));
			}
		}

		private List<Wacom.Devices.RealTimePointReceivedEventArgs> _realTimeInk_PenData = new List<Wacom.Devices.RealTimePointReceivedEventArgs>();
		private Wacom.Devices.RealTimePointReceivedEventArgs _realTimeInk_PenData_Last;

		public bool RealTimeInk_StartStop
		{
			get => _realTimeInkService?.IsStarted ?? false;
			set
			{
				Task.Run(async () =>
				{
					if (value)
					{
						_realTimeInk_PenData.Clear();
						_realTimeInk_PenData_Last = null;
						_realTimeInkService.PointReceived += _realTimeInkService_PointReceived;
						await _realTimeInkService.StartAsync(_cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						_realTimeInk_PenData_Last = null;
						await _realTimeInkService.StopAsync(_cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
						_realTimeInkService.PointReceived -= _realTimeInkService_PointReceived;
					}
					OnPropertyChanged(nameof(RealTimeInk_StartStop));
					OnPropertyChanged(nameof(RealTimeInk_IsStarted));
					OnPenDataPropertyChanged();
				}, _cancellationToken.Token);
			}
		}

		public string RealTimeInk_IsStarted => (_realTimeInkService?.IsStarted ?? false) ? "YES" : "NO";

		private void OnPenDataPropertyChanged()
		{
			_synchronizationContext.Post(OnPenDataPropertyChangedSync, null);

			void OnPenDataPropertyChangedSync(object _) // This must be called on the UI thread.
			{
				var last = _realTimeInk_PenData_Last;

				if (PropertyChanged != null)
				{
					PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(RealTimeInk_Count)));

					PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(RealTimeInk_Timestamp)));
					PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(RealTimeInk_Point)));
					PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(RealTimeInk_Phase)));

					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_Pressure, last?.Pressure, nameof(RealTimeInk_Pressure));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_PointRaw, last?.PointRaw, nameof(RealTimeInk_PointRaw));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_PressureRaw, last?.PressureRaw, nameof(RealTimeInk_PressureRaw));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_TimestampRaw, last?.TimestampRaw, nameof(RealTimeInk_TimestampRaw));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_Sequence, last?.Sequence, nameof(RealTimeInk_Sequence));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_Rotation, last?.Rotation, nameof(RealTimeInk_Rotation));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_Azimuth, last?.Azimuth, nameof(RealTimeInk_Azimuth));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_Altitude, last?.Altitude, nameof(RealTimeInk_Altitude));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_Tilt, last?.Tilt, nameof(RealTimeInk_Tilt));
					OnPenDataPropertyChangedSyncItem(tbRealTimeInk_PenId, last?.PenId, nameof(RealTimeInk_PenId));
				}

				void OnPenDataPropertyChangedSyncItem<T>(TextBlock textBlock, T? value, string name) where T : struct
				{
					bool hasValue = value != null && value.HasValue;
					if (textBlock.IsEnabled || hasValue)
					{
						textBlock.IsEnabled = hasValue;
						PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
					}
				}
			}
		}

		public float RealTimeInk_Transform_M11
		{
			get => _realTimeInkService?.PointTransform.M11 ?? float.NaN;
			set { var v = _realTimeInkService.PointTransform; v.M11 = value; _realTimeInkService.PointTransform = v; OnPropertyChanged(nameof(RealTimeInk_Transform_M11)); }
		}

		public float RealTimeInk_Transform_M12
		{
			get => _realTimeInkService?.PointTransform.M12 ?? float.NaN;
			set { var v = _realTimeInkService.PointTransform; v.M12 = value; _realTimeInkService.PointTransform = v; OnPropertyChanged(nameof(RealTimeInk_Transform_M12)); }
		}
		public float RealTimeInk_Transform_M21
		{
			get => _realTimeInkService?.PointTransform.M21 ?? float.NaN;
			set { var v = _realTimeInkService.PointTransform; v.M21 = value; _realTimeInkService.PointTransform = v; OnPropertyChanged(nameof(RealTimeInk_Transform_M21)); }
		}
		public float RealTimeInk_Transform_M22
		{
			get => _realTimeInkService?.PointTransform.M22 ?? float.NaN;
			set { var v = _realTimeInkService.PointTransform; v.M22 = value; _realTimeInkService.PointTransform = v; OnPropertyChanged(nameof(RealTimeInk_Transform_M22)); }
		}
		public float RealTimeInk_Transform_M31
		{
			get => _realTimeInkService?.PointTransform.M31 ?? float.NaN;
			set { var v = _realTimeInkService.PointTransform; v.M31 = value; _realTimeInkService.PointTransform = v; OnPropertyChanged(nameof(RealTimeInk_Transform_M31)); }
		}
		public float RealTimeInk_Transform_M32
		{
			get => _realTimeInkService?.PointTransform.M32 ?? float.NaN;
			set { var v = _realTimeInkService.PointTransform; v.M32 = value; _realTimeInkService.PointTransform = v; OnPropertyChanged(nameof(RealTimeInk_Transform_M32)); }
		}

		private void _realTimeInkService_PointReceived(object sender, Wacom.Devices.RealTimePointReceivedEventArgs e)
		{
			_realTimeInk_PenData_Last = e;
			_realTimeInk_PenData.Add(_realTimeInk_PenData_Last);
			OnPenDataPropertyChanged();
		}

		public int RealTimeInk_Count => _realTimeInk_PenData.Count;

		private void RealTimeInk_PenData_Clear(object sender, RoutedEventArgs e)
		{
			_realTimeInk_PenData_Last = null;
			_realTimeInk_PenData.Clear();
			OnPenDataPropertyChanged();
		}

		private void RealTimeInk_SavePenData(string fileName)
		{
			try
			{
				using var stream = File.CreateText(fileName);
				stream.WriteLine("Timestamp,PointX,PointY,Phase,Pressure,PointDisplayX,PointDisplayY,PointRawX,PointRawY,PressureRaw,TimestampRaw,Sequence,Rotation,Azimuth,Altitude,TiltX,TiltY,PenId");

				StringBuilder sb = new StringBuilder();
				foreach (var item in _realTimeInk_PenData)
				{
					sb.Append($"{item.Timestamp.ToString("O")},{item.Point.X,6},{item.Point.Y,6},{item.Phase.ToString(),-11}");
					sb.Append(item.Pressure.HasValue ? $",{item.Pressure.Value,9}" : ",");

					sb.Append(item.PointDisplay.HasValue ? $",{item.PointDisplay.Value.X,6},{item.PointDisplay.Value.Y,6}" : ",,");
					sb.Append(item.PointRaw.HasValue ? $",{item.PointRaw.Value.X,6},{item.PointRaw.Value.Y,6}" : ",,");
					sb.Append(item.PressureRaw.HasValue ? $",{item.PressureRaw.Value,6}" : ",");
					sb.Append(item.TimestampRaw.HasValue ? $",{item.TimestampRaw.Value,8}" : ",");
					sb.Append(item.Sequence.HasValue ? $",{item.Sequence.Value,8}" : ",");
					sb.Append(item.Rotation.HasValue ? $",{item.Rotation.Value,9}" : ",");
					sb.Append(item.Azimuth.HasValue ? $",{item.Azimuth.Value,9}" : ",");
					sb.Append(item.Altitude.HasValue ? $",{item.Altitude.Value,9}" : ",");
					sb.Append(item.Tilt.HasValue ? $",{item.Tilt.Value.X,9},{item.Tilt.Value.Y,9}" : ",,");
					sb.Append(item.PenId.HasValue ? $",0x{item.PenId.Value:x8}" : ",");
					stream.WriteLine(sb.ToString());
					sb.Clear();
				}
				stream.Close();
			}
			catch (Exception ex)
			{
				_synchronizationContext.Post(o => MessageBox.Show($"Unable to load image: {ex.Message}"), null);
			}
		}

		private void RealTimeInk_PenData_Save(object sender, RoutedEventArgs e)
		{
			if (_realTimeInk_PenData.Count > 0)
			{
				SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Title = "Save PenData",
					AddExtension = true,
					DefaultExt = "csv",
					Filter = "CSV (*.csv)|*.csv",
					OverwritePrompt = true
				};

				if (saveFileDialog.ShowDialog() == true)
				{
					Task.Run(() => RealTimeInk_SavePenData(saveFileDialog.FileName), _cancellationToken.Token);
				}
			}
		}

		private string ValueToString<T>(T? value) where T : struct
		{
			return value != null && value.HasValue ? value.ToString() : "";
		}
		private string ValueToHexString(int? value)
		{
			return value != null && value.HasValue ? $"0x{value.Value:x8}" : "";
		}

		public string RealTimeInk_Timestamp => _realTimeInk_PenData_Last?.Timestamp.ToString("O");
		public string RealTimeInk_Point => _realTimeInk_PenData_Last?.Point.ToString();
		public string RealTimeInk_Phase => _realTimeInk_PenData_Last?.Phase.ToString();
		public string RealTimeInk_Pressure => ValueToString(_realTimeInk_PenData_Last?.Pressure);
		public string RealTimeInk_PointDisplay => ValueToString(_realTimeInk_PenData_Last?.PointDisplay);
		public string RealTimeInk_PointRaw => ValueToString(_realTimeInk_PenData_Last?.PointRaw);
		public string RealTimeInk_PressureRaw => ValueToString(_realTimeInk_PenData_Last?.PressureRaw);
		public string RealTimeInk_TimestampRaw => ValueToString(_realTimeInk_PenData_Last?.TimestampRaw);
		public string RealTimeInk_Sequence => ValueToString(_realTimeInk_PenData_Last?.Sequence);
		public string RealTimeInk_Rotation => ValueToString(_realTimeInk_PenData_Last?.Rotation);
		public string RealTimeInk_Azimuth => ValueToString(_realTimeInk_PenData_Last?.Azimuth);
		public string RealTimeInk_Altitude => ValueToString(_realTimeInk_PenData_Last?.Altitude);
		public string RealTimeInk_Tilt => ValueToString(_realTimeInk_PenData_Last?.Tilt);
		public string RealTimeInk_PenId => ValueToHexString(_realTimeInk_PenData_Last?.PenId);
		#endregion

		#region Encryption Tab
		public void Initialize_Encryption()
		{
			OnPropertyChanged(nameof(Encryption_CipherSuite));
		}

		public bool Encryption_StartStop
		{
			get => _encryptionService?.IsStarted ?? false;
			set
			{
				Task.Run(async () =>
				{
					if (value)
					{
						await _encryptionService.StartAsync(_cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						await _encryptionService.StopAsync(_cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					OnPropertyChanged(nameof(Encryption_StartStop));
					OnPropertyChanged(nameof(Encryption_IsStarted));
					OnPropertyChanged(nameof(Encryption_CipherSuite));
				}, _cancellationToken.Token);
			}
		}

		public string Encryption_IsStarted => (_encryptionService?.IsStarted ?? false) ? "YES" : "NO";

		public string Encryption_CipherSuite => _encryptionService?.CipherSuite;

		#endregion

		#region FileTransfer Tab

		public class FileTransfer_InkDocumentItem
		{
			private Wacom.Devices.IInkDocument _inkDocument;
			private Exception _fileTransferException;

			public FileTransfer_InkDocumentItem(Wacom.Devices.IInkDocument inkDocument, Exception fileTransferException)
			{
				_inkDocument = inkDocument;
				_fileTransferException = fileTransferException;
			}

			private int TotalStrokesCount()
			{
				int strokesCount = 0;

				foreach (var layer in InkDocument.Layers)
				{
					strokesCount += layer.Strokes.Count;
				}
				return strokesCount;
			}

			public Wacom.Devices.IInkDocument InkDocument => _inkDocument;
			public string Description => (InkDocument != null) ? $"{InkDocument.CreationDate.ToString()} ({InkDocument.Layers.Count} layers, {TotalStrokesCount()} total strokes)" : _fileTransferException.Message;
		};

		private ObservableCollection<FileTransfer_InkDocumentItem> _fileTransfer_InkDocuments = new ObservableCollection<FileTransfer_InkDocumentItem>();
		private ObservableCollection<InkDocumentViewModel> _fileTransfer_InkDocument = new ObservableCollection<InkDocumentViewModel>();
		private string _fileTransfer_Message;
		private int _fileTransfer_retryCounter = 0;

		private void Initialize_FileTransfer()
		{
			_synchronizationContext.Post(FileTransfer_InitializeSync, null);

			void FileTransfer_InitializeSync(object _)
			{
				OnPropertyChangedSync(nameof(FileTransfer_IsStarted));
				OnPropertyChangedSync(nameof(FileTransfer_Transform_M11));
				OnPropertyChangedSync(nameof(FileTransfer_Transform_M12));
				OnPropertyChangedSync(nameof(FileTransfer_Transform_M21));
				OnPropertyChangedSync(nameof(FileTransfer_Transform_M22));
				OnPropertyChangedSync(nameof(FileTransfer_Transform_M31));
				OnPropertyChangedSync(nameof(FileTransfer_Transform_M32));
			}
		}

		public ObservableCollection<FileTransfer_InkDocumentItem> FileTransfer_InkDocuments => _fileTransfer_InkDocuments;
		public ObservableCollection<InkDocumentViewModel> FileTransfer_InkDocument => _fileTransfer_InkDocument;
		public string FileTransfer_Message => _fileTransfer_Message;


		public bool FileTransfer_StartStop
		{
			get => _fileTransferService?.IsStarted ?? false;
			set
			{
				Task.Run(async () =>
				{
					if (value)
					{
						_fileTransferService.ServiceError += FileTransfer_ServiceError;
						_fileTransferService.StartingFileDownload += FileTransfer_StartingFileDownload;
						await _fileTransferService.StartAsync(FileTransfer_ReceiveFilesAsync, _cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						await _fileTransferService.StopAsync(_cancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
						_fileTransferService.ServiceError -= FileTransfer_ServiceError;
						_fileTransferService.StartingFileDownload -= FileTransfer_StartingFileDownload;
					}
					OnPropertyChanged(nameof(FileTransfer_StartStop));
					OnPropertyChanged(nameof(FileTransfer_IsStarted));
				}, _cancellationToken.Token);
			}
		}

		private void FileTransfer_StartingFileDownload(object sender, Wacom.Devices.StartingFileDownloadEventArgs e)
		{
			_fileTransfer_Message = $"Downloading {e.FilesCount} files...";
			OnPropertyChanged(nameof(FileTransfer_Message));
		}

		private void FileTransfer_ServiceError(object sender, Wacom.Devices.ServiceErrorEventArgs e)
		{
			_fileTransfer_Message = e.Exception.Message;
			OnPropertyChanged(nameof(FileTransfer_Message));
		}

		private Task<Wacom.Devices.FileTransferControlOptions> FileTransfer_ReceiveFilesAsync(Wacom.Devices.IInkDocument inkDocument, Exception fileTransferException)
		{
			if (fileTransferException != null)
			{
				_fileTransfer_Message = fileTransferException.Message;
				OnPropertyChanged(nameof(FileTransfer_Message));

				if (_fileTransfer_retryCounter < 3)
				{
					++_fileTransfer_retryCounter;
					return Task.FromResult(Wacom.Devices.FileTransferControlOptions.Retry);
				}
				else
				{
					_fileTransfer_retryCounter = 0;
					return Task.FromResult(Wacom.Devices.FileTransferControlOptions.Continue);
				}
			}
			else
			{
				_fileTransfer_Message = null;
			}

			_synchronizationContext.Post(o => { _fileTransfer_InkDocuments.Add(new FileTransfer_InkDocumentItem(inkDocument, fileTransferException)); OnPropertyChangedSync(nameof(FileTransfer_Message)); }, null);

			return Task.FromResult(Wacom.Devices.FileTransferControlOptions.Continue);
		}

		public string FileTransfer_IsStarted => (_fileTransferService?.IsStarted ?? false) ? "YES" : "NO";

		public float FileTransfer_Transform_M11
		{
			get => _fileTransferService?.Transform.M11 ?? float.NaN;
			set { var v = _fileTransferService.Transform; v.M11 = value; _fileTransferService.Transform = v; OnPropertyChanged(nameof(FileTransfer_Transform_M11)); }
		}

		public float FileTransfer_Transform_M12
		{
			get => _fileTransferService?.Transform.M12 ?? float.NaN;
			set { var v = _fileTransferService.Transform; v.M12 = value; _fileTransferService.Transform = v; OnPropertyChanged(nameof(FileTransfer_Transform_M12)); }
		}
		public float FileTransfer_Transform_M21
		{
			get => _fileTransferService?.Transform.M21 ?? float.NaN;
			set { var v = _fileTransferService.Transform; v.M21 = value; _fileTransferService.Transform = v; OnPropertyChanged(nameof(FileTransfer_Transform_M21)); }
		}
		public float FileTransfer_Transform_M22
		{
			get => _fileTransferService?.Transform.M22 ?? float.NaN;
			set { var v = _fileTransferService.Transform; v.M22 = value; _fileTransferService.Transform = v; OnPropertyChanged(nameof(FileTransfer_Transform_M22)); }
		}
		public float FileTransfer_Transform_M31
		{
			get => _fileTransferService?.Transform.M31 ?? float.NaN;
			set { var v = _fileTransferService.Transform; v.M31 = value; _fileTransferService.Transform = v; OnPropertyChanged(nameof(FileTransfer_Transform_M31)); }
		}
		public float FileTransfer_Transform_M32
		{
			get => _fileTransferService?.Transform.M32 ?? float.NaN;
			set { var v = _fileTransferService.Transform; v.M32 = value; _fileTransferService.Transform = v; OnPropertyChanged(nameof(FileTransfer_Transform_M32)); }
		}

		public uint FileTransfer_SyncInterval
		{
			get => _fileTransferService?.SyncInterval ?? 0;
			set { _fileTransferService.SyncInterval = value; OnPropertyChanged(nameof(FileTransfer_SyncInterval)); }
		}


		private void FileTransfer_DocumentSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var doc = e.AddedItems[0] as FileTransfer_InkDocumentItem;

			var inkDocumentViewModel = doc != null ? new InkDocumentViewModel(doc.InkDocument) : null;

			_synchronizationContext.Post(o => { _fileTransfer_InkDocument.Clear(); if (inkDocumentViewModel != null) _fileTransfer_InkDocument.Add(inkDocumentViewModel); }, null);
		}


		#endregion

	}

}
