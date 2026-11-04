using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using ConfuserNext;

namespace ConfuserNext.ViewModel {
	internal class AboutTabVM : TabViewModel {
		public AboutTabVM(AppVM app)
			: base(app, "About") {
			var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/ConfuserNext.ico"), BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);

			Icon = decoder.Frames.First(frame => frame.Width == 64);
		}

		public static ICommand LaunchBrowser {
			get { return new RelayCommand<string>(site => Process.Start(site)); }
		}

		public BitmapSource Icon { get; private set; }
	}
}