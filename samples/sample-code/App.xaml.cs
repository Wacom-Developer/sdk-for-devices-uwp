using System;
using System.Configuration;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Demo.WPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{

		#region AppId (used by BLE devices for pairing)
		private class MyAppId : Wacom.Devices.IApplicationIdentifier
		{
			public byte[] ToByteArray() => new byte[] { 0xFA, 0xAB, 0xC1, 0xE0, 0xF1, 0x77 };
		};

		static internal Wacom.Devices.IApplicationIdentifier AppId => new MyAppId();
		#endregion

		#region Transport Images
		private static readonly ImageSource[] _images = new[]
		{
			null, // unknown
			new BitmapImage(new Uri("pack://application:,,,/Resources/ble.png")),
			null, // btc
			new BitmapImage(new Uri("pack://application:,,,/Resources/usb.png")),
			new BitmapImage(new Uri("pack://application:,,,/Resources/hid.png")),
			null, // serial
			new BitmapImage(new Uri("pack://application:,,,/Resources/wac.png"))
		};

		internal static ImageSource TransportImage(Wacom.Devices.TransportProtocol transportProtocol)
		{
			return _images[(int)transportProtocol];
		}
		#endregion

		#region DiscreteDisplayService Sample Image
		internal static System.Drawing.Bitmap DiscreteDisplaySampleImage()
		{
			var encoder = new PngBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(new BitmapImage(new Uri("pack://application:,,,/Resources/DiscreteDisplaySampleImage.png"))));
			using var stream = new System.IO.MemoryStream();
			encoder.Save(stream);
			stream.Flush();
			return new System.Drawing.Bitmap(stream);
		}
		#endregion

		#region Initial Wacom Licensing
		private static void InitializeWacomLicense()
		{
			var license = ConfigurationManager.AppSettings["Wacom.License"];
			if (license == null || license.Length == 0)
				license = Environment.GetEnvironmentVariable("Wacom.License");

			if (license != null && license.Length > 0)
			{
				Wacom.Licensing.LicenseValidator.Instance.SetLicense(license);
				if (DateTime.Now > Wacom.Licensing.LicenseValidator.Instance.ExpiryDate())
				{
					MessageBox.Show("The license has expired, please obtain a new one");
				}
			}
			else
			{
				MessageBox.Show("No license found. Please add to app.config or as an environment variable called \"Wacom.License\".");
			}
		}
		#endregion

		public App()
		{
			Startup += (s, e) => InitializeWacomLicense();
		}
	}
}
