﻿using CSCore.CoreAudioAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
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

namespace VPet.Plugin.MultiChannelMusicFix
{
    /// <summary>
    /// SettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DebugWindow : Window
    {
        public MultiChannelMusicFix master;
        
        public DebugWindow(MultiChannelMusicFix _master)
        {
            InitializeComponent();
            master = _master;

            //foreach (var device in MultiChannelMusicFix.activeAllDevices) { textBox.Text += device.FriendlyName + "\n"; }
        }

        private void Window_Closed(object sender, EventArgs e) 
        {
            master.debugWindow = null;
        }
    }
}
