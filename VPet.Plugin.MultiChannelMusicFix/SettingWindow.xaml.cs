using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Panuon.WPF.UI;
using VPet_Simulator.Core;
using VPet_Simulator.Windows;
using LinePutScript.Localization.WPF;
using System.Timers;
using CSCore.CoreAudioAPI;

namespace VPet.Plugin.MultiChannelMusicFix
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow : Window
    {
        private bool initialized = false;

        public MultiChannelMusicFix master;

        private Timer sliderSaveTimer = new Timer();

        private static List<MediaDevice> speakers = new List<MediaDevice>();
        public static IEnumerable<MediaDevice> Speakers
        {
            get {
                return speakers;
            }
            set {
                speakers = MultiChannelMusicFix.activeRenderDevices.Except(MultiChannelMusicFix.activeCaptureDevices, new AudioDeviceComparer()).Select<MMDevice, MediaDevice>((device) => { return new MediaDevice { Name = device.FriendlyName, DeviceId = device.DeviceID }; }).ToList();
            }
        }

        private static List<MediaDevice> microphones = new List<MediaDevice>();
        public static IEnumerable<MediaDevice> MicroPhones
        {
            get {
                return microphones;
            }
            set {
                microphones = MultiChannelMusicFix.activeCaptureDevices.Select<MMDevice, MediaDevice>((device) => { return new MediaDevice { Name = device.FriendlyName, DeviceId = device.DeviceID }; }).ToList();
            }
        }

        public SettingWindow(MultiChannelMusicFix _master)
        {
            InitializeComponent();
            master = _master;

            sliderSaveTimer.Interval = 300;
            sliderSaveTimer.Enabled = false;
            sliderSaveTimer.AutoReset = false;
            sliderSaveTimer.Elapsed += (o, e) => {
                MultiChannelMusicFix.config.SaveConfig(MultiChannelMusicFix.pluginDirectory);
            };

            MicrophoneCaptureSwitch.IsChecked = MultiChannelMusicFix.config.IsCaptureMicrophone;
            ComboBox_Speaker.SelectedItem = Speakers.SingleOrDefault<MediaDevice>((device) => { return device.DeviceId == MultiChannelMusicFix.config.SelectedSpeakerDeviceId; }, null);
            ComboBox_Microphones.SelectedItem = MicroPhones.SingleOrDefault<MediaDevice>((device) => { return device.DeviceId == MultiChannelMusicFix.config.SelectedMicrophoneDeviceId; }, null);

            initialized = true;
        }

        public void Window_Closed(object sender, EventArgs e) 
        {
            master.settingWindow = null;
        }

        private void MicrophoneCaptureSwitch_Checked(object sender, RoutedEventArgs e)
        {
            if (!initialized) return;
            MultiChannelMusicFix.config.SaveConfig(MultiChannelMusicFix.pluginDirectory);
        }

        private void MicrophoneCaptureSwitch_UnChecked(object sender, RoutedEventArgs e)
        {
            if (!initialized) return;
            MultiChannelMusicFix.config.SaveConfig(MultiChannelMusicFix.pluginDirectory);
        }

        private void Slider_SpeakerCaptureVolumeMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TextBlock_SpeakerCaptureVolumeMultiplier == null) return;
            TextBlock_SpeakerCaptureVolumeMultiplier.Text = "x" + MultiChannelMusicFix.config.SpeakerCaptureVolumeMultiplier.ToString("0.00");

            if (!initialized) return;
            if (sliderSaveTimer.Enabled) { sliderSaveTimer.Stop(); }
            sliderSaveTimer.Start();
        }

        private void Slider_SpeakerCaptureVolumeMultiplier_Loaded(object sender, RoutedEventArgs e)
        {
            TextBlock_SpeakerCaptureVolumeMultiplier.Text = "x" + MultiChannelMusicFix.config.SpeakerCaptureVolumeMultiplier.ToString("0.00");
        }

        private void Slider_MicrophoneCaptureVolumeMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TextBlock_MicrophoneCaptureVolumeMultiplier == null) return;
            TextBlock_MicrophoneCaptureVolumeMultiplier.Text = "x" + MultiChannelMusicFix.config.MicrophoneCaptureVolumeMultiplier.ToString("0.00");

            if (!initialized) return;
            if (sliderSaveTimer.Enabled) { sliderSaveTimer.Stop(); }
            sliderSaveTimer.Start();
        }

        private void Slider_MicrophoneCaptureVolumeMultiplier_Loaded(object sender, RoutedEventArgs e)
        {
            TextBlock_MicrophoneCaptureVolumeMultiplier.Text = "x" + MultiChannelMusicFix.config.MicrophoneCaptureVolumeMultiplier.ToString("0.00");
        }

        private void ComboBox_Speaker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MultiChannelMusicFix.config.SelectedSpeakerDeviceId = ((MediaDevice)ComboBox_Speaker.SelectedItem).DeviceId;
            if (!initialized) return;
            MultiChannelMusicFix.config.SaveConfig(MultiChannelMusicFix.pluginDirectory);
        }

        private void ComboBox_Microphones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MultiChannelMusicFix.config.SelectedMicrophoneDeviceId = ((MediaDevice)ComboBox_Microphones.SelectedItem).DeviceId;
            if (!initialized) return;
            MultiChannelMusicFix.config.SaveConfig(MultiChannelMusicFix.pluginDirectory);
        }

        public class MediaDevice
        {
            private string name;
            public string Name
            {
                get { return name; }
                set { name = value; }
            }

            private string deviceId;

            public string DeviceId
            {
                get { return deviceId; }
                set { deviceId = value; }
            }

            public override bool Equals(object obj)
            {
                return deviceId.Equals(((MediaDevice)obj).deviceId);
            }

            public override int GetHashCode()
            {
                return deviceId.GetHashCode();
            }
        }
    }
}
