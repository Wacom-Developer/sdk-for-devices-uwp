using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Wacom.Devices;
using Wacom.SmartPadCommunication;
using Windows.Storage;
using Windows.UI.Xaml.Media;
using Windows.UI.Popups;

namespace WillDevicesSampleApp
{
	public class AppObjects
	{
		public static readonly AppObjects Instance = new AppObjects();
		private static readonly string SaveFileName = "SavedData";

		private AppObjects()
		{
			AppId = new SmartPadClientId(0xFA, 0xAB, 0xC1, 0xE0, 0xF1, 0x77);
		}

		public IDigitalInkDevice Device
		{
			get;
			set;
		}

		public SmartPadClientId AppId
		{
			get;
		}

		public InkDeviceInfo DeviceInfo
		{
			get;
			set;
		}

		public static async Task SerializeDeviceInfoAsync(InkDeviceInfo deviceInfo)
		{
			try
			{
				StorageFile storageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(SaveFileName, CreationCollisionOption.ReplaceExisting);

				using (Stream stream = await storageFile.OpenStreamForWriteAsync())
				{
					deviceInfo.ToStream(stream);
				}
			}
			catch (Exception)
			{
			}
		}

		public static async Task<InkDeviceInfo> DeserializeDeviceInfoAsync()
		{
			try
			{
				StorageFile storageFile = await ApplicationData.Current.LocalFolder.GetFileAsync(SaveFileName);

				using (Stream stream = await storageFile.OpenStreamForReadAsync())
				{
					return await InkDeviceInfo.FromStreamAsync(stream);
				}
			}
			catch (Exception)
			{
			}

			return null;
		}

		public static Matrix CalculateTransform(uint deviceWidth, uint deviceHeight, uint ptSizeInMicrometers)
		{
			float scaleFactor = ptSizeInMicrometers * micrometerToDip;

			ScaleTransform st = new ScaleTransform();
			st.ScaleX = scaleFactor;
			st.ScaleY = scaleFactor;

			RotateTransform rt = new RotateTransform();
			rt.Angle = 90;

			TranslateTransform tt = new TranslateTransform();
			tt.X = deviceHeight * scaleFactor;
			tt.Y = 0;

			TransformGroup tg = new TransformGroup();
			tg.Children.Add(st);
			tg.Children.Add(rt);
			tg.Children.Add(tt);

			return tg.Value;
		}

		public static string GetStringForDeviceStatus(DeviceStatus deviceStatus)
		{
			string text = string.Empty;

			switch (deviceStatus)
			{
				case DeviceStatus.Idle:
					break;

				case DeviceStatus.Reconnecting:
					text = "Connecting...";
					break;

				case DeviceStatus.Syncing:
					text = "Syncing...";
					break;

				case DeviceStatus.CapturingRealTimeInk:
					text = "Real time ink mode enabled.";
					break;

				case DeviceStatus.ExpectingConnectionConfirmation:
					text = "Tap the Central Button to confirm the connection.";
					break;

				case DeviceStatus.ExpectingReconnect:
					text = "Tap the Central Button to restore the connection.";
					break;

				case DeviceStatus.ExpectingUserConfirmationMode:
					text = "Press and hold the Central Button to enter user confirmation mode.";
					break;

				case DeviceStatus.NotAuthorizedConnectionNotConfirmed:
					text = "The connection confirmation period expired.";
					break;

				case DeviceStatus.NotAuthorizedDeviceInUseByAnotherHost:
					text = "The device is in use by another host.";
					break;

				case DeviceStatus.NotAuthorizedGeneralError:
					text = "The device authorization failed.";
					break;
			}

			return text;
		}

		public async Task<bool> ShowPairingModeEnabledDialogAsync()
		{
			var dialog = new MessageDialog($"The device {DeviceInfo.DeviceName} is in pairing mode. How do you want proceed?");
			dialog.Commands.Add(new UICommand("Keep using the device") { Id = 0 });
			dialog.Commands.Add(new UICommand("Forget the device") { Id = 1 });

			var dialogResult = await dialog.ShowAsync();

			return ((int)dialogResult.Id == 0);
		}

		public const float micrometerToDip = 96.0f / 25400.0f;
	}
}