using System.Windows;
using System.ComponentModel;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Collections.ObjectModel;


namespace Demo.WPF
{
	public class DeviceProperty
	{
		private readonly string _name;
		private readonly Formatter _formatter;

		public delegate string Formatter(object o);

		public string Name => _name;

		public string Format(object o)
		{
			if (o != null)
			{
				return _formatter != null ? _formatter(o) : o.ToString();
			}
			else
			{
				return null;
			}
		}

		public DeviceProperty(DeviceProperty other)
		{
			_name = other._name;
			_formatter = other._formatter;
		}

		public DeviceProperty(string name)
		{
			_name = name;
			_formatter = null;
		}
		public DeviceProperty(string name, Formatter formatter)
		{
			_name = name;
			_formatter = formatter;
		}
	}

	public class DevicePropertyValue : DeviceProperty, INotifyPropertyChanged
	{
		private string _value;

		public string Value
		{
			get => _value ?? "N/A";
			private set
			{
				if (_value != null || value != null)
				{
					_value = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public DevicePropertyValue(DeviceProperty deviceProperty)
			: base(deviceProperty)
		{
		}

		public void SetValue(object o)
		{
			Value = Format(o);
		}
	}


	public static class DeviceProperties
	{
		#region Customer Formatters
		static private string Formatter(Wacom.Devices.Types.SmartPad.BatteryState v) => $"{v.Percentage}% " + (v.IsCharging ? "Charging" : "Not charging");
		private static string Formatter(Wacom.Devices.Types.WacomDriver.FirmwareVersion v) => v.AsString;

		private static string Formatter(Wacom.Devices.Types.WacomDriver.RangeF v) => $"{{Min={v.Min} Max={v.Max} Res={v.Resolution}}}";
		private static string Formatter(Wacom.Devices.Types.WacomDriver.RectF v) => $"{{X={v.X} Y={v.Y} W={v.Width} H={v.Height}}}";
		private static string Formatter(Wacom.Devices.Types.WacomDriver.MappingF v) => $"Raw={Formatter(v.Raw)} Active={Formatter(v.Active)} Physical={Formatter(v.Physical)} Orientation={v.Orientation.ToString()}";
		private static string Formatter(Wacom.Devices.Types.WacomDriver.Capability v) => $"{v.Name}={Formatter(v.Range)}";
		private static string Formatter(ICollection<Wacom.Devices.Types.WacomDriver.Capability> v)
		{
			var sb = new System.Text.StringBuilder();
			bool first = true;
			foreach (var c in v)
			{
				if (first)
					first = false;
				else
					sb.Append(", ");
				sb.Append(Formatter(c));
			}
			return sb.ToString();
		}
		#endregion

		static public readonly DeviceProperty[] Device = new[]
		{
			new DeviceProperty(Wacom.Devices.Properties.Device.Name),
			new DeviceProperty(Wacom.Devices.Properties.Device.SerialNumber),
			new DeviceProperty(Wacom.Devices.Properties.Device.Width),
			new DeviceProperty(Wacom.Devices.Properties.Device.Height),
			new DeviceProperty(Wacom.Devices.Properties.Device.Orientation),
			new DeviceProperty(Wacom.Devices.Properties.Device.FirmwareVersion),
			new DeviceProperty(Wacom.Devices.Properties.Device.HidInformation)
		};

		static public readonly DeviceProperty[] Digitizer = new[]
		{
			new DeviceProperty(Wacom.Devices.Properties.Digitizer.Width),
			new DeviceProperty(Wacom.Devices.Properties.Digitizer.Height),
			new DeviceProperty(Wacom.Devices.Properties.Digitizer.Orientation),
			new DeviceProperty(Wacom.Devices.Properties.Digitizer.Resolution),
			new DeviceProperty(Wacom.Devices.Properties.Digitizer.SamplingRate)
		};

		static public readonly DeviceProperty[] Screen = new[]
		{
				new DeviceProperty(Wacom.Devices.Properties.Screen.Type),
				new DeviceProperty(Wacom.Devices.Properties.Screen.Width),
				new DeviceProperty(Wacom.Devices.Properties.Screen.Height),
				new DeviceProperty(Wacom.Devices.Properties.Screen.Orientation),
				new DeviceProperty(Wacom.Devices.Properties.Screen.ResolutionX),
				new DeviceProperty(Wacom.Devices.Properties.Screen.ResolutionY)
		};

		static public readonly DeviceProperty[] STU = new[]
		{
				new DeviceProperty(Wacom.Devices.Properties.STU.Status),
				new DeviceProperty(Wacom.Devices.Properties.STU.Uid),
				new DeviceProperty(Wacom.Devices.Properties.STU.Uid2),
				new DeviceProperty(Wacom.Devices.Properties.STU.Eserial),
				new DeviceProperty(Wacom.Devices.Properties.STU.BackgroundColor),
				new DeviceProperty(Wacom.Devices.Properties.STU.BacklightBrightness),
				new DeviceProperty(Wacom.Devices.Properties.STU.BootScreen),
				new DeviceProperty(Wacom.Devices.Properties.STU.DefaultMode),
				new DeviceProperty(Wacom.Devices.Properties.STU.HandwritingThicknessColor),
				new DeviceProperty(Wacom.Devices.Properties.STU.InkingMode),
				new DeviceProperty(Wacom.Devices.Properties.STU.InkThreshold),
				new DeviceProperty(Wacom.Devices.Properties.STU.RenderingMode),
				new DeviceProperty(Wacom.Devices.Properties.STU.ScreenContrast),
				new DeviceProperty(Wacom.Devices.Properties.STU.HandwritingDisplayArea)
		};

		static public readonly DeviceProperty[] SmartPad = new[]
		{
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.BatteryLevelReportChange),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.BatteryState, o => Formatter(o as Wacom.Devices.Types.SmartPad.BatteryState)),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.ConnectionInterval),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.DataSessionAcceptDuration),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.EnableDataEncryption),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.FileTransferServiceReportingType),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.FirmwareProtocolLevel),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.HoveringDataOutput),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.PenDetectedIndicationLedMode),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.PenDetectedIndicationSoundEffect),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.PenDetectedIndicationSoundVol),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.PenDetectedNotificationFlag),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.RealTimeServiceReportingType),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.ReportDataSessionEvents),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.UserConfirmationStartAckDuration),
				new DeviceProperty(Wacom.Devices.Properties.SmartPad.UserConfirmationTimeout)
		};

		static public readonly DeviceProperty[] WacomDriver = new[]
		{
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.SystemId),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.UniqueId),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.FriendlyName),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.ModelNumber),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.IdVendor),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.IdProduct),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.BcdVersion),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.SerialNumber),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.PenFirmwareVersion, o => Formatter(o as Wacom.Devices.Types.WacomDriver.FirmwareVersion)),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.Connection),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.IsEncrypted),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.TabletExtent, o => Formatter(o as Wacom.Devices.Types.WacomDriver.MappingF)),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.DisplayExtent, o => Formatter(o as Wacom.Devices.Types.WacomDriver.MappingF)),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.Display),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.ScreenId),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.Capabilities, o => Formatter(o as ICollection<Wacom.Devices.Types.WacomDriver.Capability>)),
				new DeviceProperty(Wacom.Devices.Properties.WacomDriver.PointsPerSecond)
		};

	}
}