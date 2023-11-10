using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading;
using Microsoft.Win32;

namespace Demo.WPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	///
	public partial class MainWindow : Window
	{
		private readonly SynchronizationContext _synchronizationContext;
		private List<Wacom.Devices.IInkDeviceWatcher> _inkDeviceWatchers = new List<Wacom.Devices.IInkDeviceWatcher>();
		private ObservableCollection<Connection> _connectionList = new ObservableCollection<Connection>();

		#region Helpers
		private Connection FindConnectionFromDeviceInfo(Wacom.Devices.IInkDeviceInfo e)
		{
			foreach (var i in _connectionList)
			{
				if (i.Id == e.Id)
					return i;
			}
			return null;
		}
		#endregion

		public MainWindow()
		{
			_synchronizationContext = SynchronizationContext.Current;

			InitializeComponent();
			InitializeInkWatchers();

			void InitializeInkWatchers()
			{
				StringBuilder sb = new StringBuilder();
				int countStarted = 0;
				for (int i = 0; i < 3; ++i)
				{
					try
					{
						Wacom.Devices.IInkDeviceWatcher inkDeviceWatcher = i switch
						{
							0 => Wacom.Devices.InkDeviceWatcher.BLE,
							1 => Wacom.Devices.InkDeviceWatcher.USB,
							2 => Wacom.Devices.InkDeviceWatcher.WAC,
							_ => throw new IndexOutOfRangeException()
						};

						_inkDeviceWatchers.Add(inkDeviceWatcher);
						inkDeviceWatcher.DeviceAdded += (s, e) => _synchronizationContext.Post(o => DeviceAdded(s, e), null);
						inkDeviceWatcher.DeviceRemoved += (s, e) => _synchronizationContext.Post(o => DeviceRemoved(s, e), null);
						inkDeviceWatcher.Start();
						++countStarted;
					}
					catch (Exception ex)
					{
						sb.AppendLine($"Unable to create InkDeviceWatcher [index={i}] {ex.Message}");
					}
				}

				if (countStarted == 0)
				{
					sb.AppendLine("No Watchers have been started");
				}

				if (sb.Length > 0)
				{
					MessageBox.Show(sb.ToString());
				}
			}

		}

		#region DeviceWatcher Events
		private void DeviceRemoved(object sender, Wacom.Devices.IInkDeviceInfo e)
		{
			var connection = FindConnectionFromDeviceInfo(e);
			if (connection != null)
			{
				_connectionList.Remove(connection);
				connection.Close();
			}
		}

		private void DeviceAdded(object sender, Wacom.Devices.IInkDeviceInfo e)
		{
			_connectionList.Add(new Connection(e));
		}
		#endregion

		#region Connect OnClick / Double Click connection handling
		private void Connect_OnClick(object sender, RoutedEventArgs e)
		{
			(lvConnectionList.SelectedItem as Connection)?.ConnectOrBringToFront();
		}

		private void lvConnectionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			(((FrameworkElement)e.OriginalSource).DataContext as Connection)?.ConnectOrBringToFront();
		}
		#endregion

		#region Load Stream handling
		private async Task LoadStreamAsync(string fileName)
		{
			Wacom.Devices.IInkDeviceInfo inkDeviceInfo = null;
			try
			{
				using FileStream fileStream = new FileStream(path: fileName, mode: FileMode.Open, access: FileAccess.Read);
				inkDeviceInfo = await Wacom.Devices.InkDeviceInfo.FromStreamAsync(fileStream).ConfigureAwait(continueOnCapturedContext:false);
			}
			catch (Exception ex)
			{
				_synchronizationContext.Post(o => MessageBox.Show($"InkDeviceInfo.FromStreamAsync failed: {ex.Message}", "Load Stream"), null);
				return;
			}

			var connection = FindConnectionFromDeviceInfo(inkDeviceInfo);
			if (connection == null)
			{
				connection = new Connection(inkDeviceInfo);
			}

			_synchronizationContext.Post(o => connection.ConnectOrBringToFront(), null);
		}

		private void LoadStream_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog()
			{
				Title = "Open Stream",
				CheckFileExists = true,
				DefaultExt = "stream",
				Filter = "Streams|*.stream|All|*.*"
			};

			if (openFileDialog.ShowDialog() == true)
			{
				Task.Run(async () => await LoadStreamAsync(openFileDialog.FileName).ConfigureAwait(continueOnCapturedContext: false));
			}
		}
		#endregion

		public ObservableCollection<Connection> ConnectionList => _connectionList;
	}
}
