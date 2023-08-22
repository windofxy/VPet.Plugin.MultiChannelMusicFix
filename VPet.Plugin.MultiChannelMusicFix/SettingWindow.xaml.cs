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

namespace VPet.Plugin.MultiChannelMusicFix
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingWindow : Window
    {
        public MultiChannelMusicFix master;

        public SettingWindow(MultiChannelMusicFix _master)
        {
            InitializeComponent();
            master = _master;

            MicrophoneCaptureSwitch.IsChecked = MultiChannelMusicFix.config.isCaptureMicrophone;
        }

        public void Window_Closed(object sender, EventArgs e) 
        {
            master.settingWindow = null;
        }

        private void MicrophoneCaptureSwitch_Checked(object sender, RoutedEventArgs e)
        {
            MultiChannelMusicFix.config.isCaptureMicrophone = true;
            MultiChannelMusicFix.config.SaveConfig(MultiChannelMusicFix.pluginDirectory);
        }

        private void MicrophoneCaptureSwitch_UnChecked(object sender, RoutedEventArgs e)
        {
            MultiChannelMusicFix.config.isCaptureMicrophone = false;
            MultiChannelMusicFix.config.SaveConfig(MultiChannelMusicFix.pluginDirectory);
        }
    }
}
