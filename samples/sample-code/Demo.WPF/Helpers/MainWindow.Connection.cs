using System;
using System.Windows.Media;

namespace Demo.WPF
{
	public class Connection
	{
		private readonly Wacom.Devices.IInkDeviceInfo _inkDeviceInfo;
		private DeviceWindow _deviceWindow;

		public string Id => _inkDeviceInfo.Id;
		public ImageSource TransportImage => App.TransportImage(_inkDeviceInfo.TransportProtocol);
		public string DeviceName => _inkDeviceInfo.DeviceName;

		public Connection(Wacom.Devices.IInkDeviceInfo inkDeviceInfo)
		{
			_inkDeviceInfo = inkDeviceInfo;
			_deviceWindow = null;
		}

		public void Close()
		{
			if (_deviceWindow != null)
			{
				_deviceWindow.Close();
				_deviceWindow = null;
			}
		}

		public void ConnectOrBringToFront()
		{
			if (_deviceWindow == null)
			{
				_deviceWindow = new DeviceWindow(_inkDeviceInfo);
				_deviceWindow.Closed += (o,e) => _deviceWindow = null;
				_deviceWindow.Show();
			}
			else
			{
				_deviceWindow.Activate();
			}
		}
	}
}