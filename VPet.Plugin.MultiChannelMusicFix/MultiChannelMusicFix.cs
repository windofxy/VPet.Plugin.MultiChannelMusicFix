using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VPet_Simulator.Windows.Interface;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CSCore.CoreAudioAPI;
using Newtonsoft.Json;
using System.IO;
using System.Windows;
using System.Net;
using HarmonyLib;
using System.Windows.Controls.Primitives;
using LinePutScript.Localization.WPF;

namespace VPet.Plugin.MultiChannelMusicFix
{
    public class MultiChannelMusicFix : MainPlugin
    {
        public override string PluginName => "多声道设备音乐识别修复&麦克风音乐检测";

        public IMainWindow mainWindow;
        public SettingWindow settingWindow = null;
        public DebugWindow debugWindow = null;

        public Assembly[] assemblies;
        public Assembly targetAssembly;
        public Type[] types;
        public Type targetType;
        public MethodInfo targetMethod;

        public static string pluginDirectory;
        public static Config config = new Config();

        public static MMDeviceEnumerator deviceEnumerator = null;
        public static MMDeviceCollection activeRenderDevices = null;
        public static MMDeviceCollection activeCaptureDevices = null;

        Harmony harmony = new Harmony("com.vpet.plugin.multichannelmusicfix");

        public MultiChannelMusicFix(IMainWindow _MainWindow) : base(_MainWindow)
        {
            mainWindow = _MainWindow;
        }

        public override void LoadPlugin()
        {
            SetupUI();

            UpdateAudioDevicesList();
            
            pluginDirectory = GetPluginDirectory();
            config = Config.ReadConfig(pluginDirectory);

            assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                if (assembly.GetName().Name == "VPet-Simulator.Windows") { targetAssembly = assembly; break; }
            }
            types = targetAssembly.GetTypes();
            foreach (Type type in types)
            {
                if (type.Name == "MainWindow") { targetType = type; break; }
            }
            targetMethod = targetType.GetMethod("AudioPlayingVolume");

            harmony.Patch(targetMethod, new HarmonyMethod(typeof(MultiChannelMusicFix).GetMethod("AudioPlayingVolumePatch")));

            if (string.IsNullOrWhiteSpace(config.SelectedSpeakerDeviceId) || (config.IsCaptureMicrophone == true && string.IsNullOrWhiteSpace(config.SelectedMicrophoneDeviceId)))
            {
                MessageBox.Show(LocalizeCore.Translate("还未选择采集设备，请前往MOD设置界面进行设置"), "VPet.Plugin.MultiChannelMusicFix", MessageBoxButton.OK);
            }

            config.SaveConfig(pluginDirectory);

#if DEBUG
            ShowDebugWindow();
#endif
        }

        public override void Setting()
        {
            UpdateAudioDevicesList();
            ShowSettingWindow();
        }

        public void SetupUI() 
        {
            mainWindow.Main.ToolBar.MenuMODConfig.Visibility = System.Windows.Visibility.Visible;
            /*
            MenuItem menuItem = new MenuItem {
                Header = "多声道设备音乐识别修复&麦克风音乐检测 Multi Channel Music Fix & Microphone Music Capture",
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
            };
            menuItem.Click += (sender, e) => { Setting(); };
            menuMODConfig.Items.Add(menuItem);
            */
        }

        public string GetPluginDirectory() 
        {
            var temp = Assembly.GetExecutingAssembly().Location;
            temp = temp.Remove(temp.LastIndexOf('\\'));
            temp = temp.Remove(temp.LastIndexOf('\\')) + "\\";
            return temp;
        }

        public static void UpdateAudioDevicesList()
        {
            try
            {
                deviceEnumerator = new MMDeviceEnumerator();
                activeCaptureDevices = deviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active);
                SettingWindow.MicroPhones = null;
                activeRenderDevices = deviceEnumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active);
                SettingWindow.Speakers = null;
            }
            finally { }
        }

        [HarmonyPrefix]
        [HarmonyPatch("AudioPlayingVolume")]
        public static bool AudioPlayingVolumePatch(ref float __result)
        {
            List<float> vols = new List<float>();
            var activeDevices = activeRenderDevices.Except(activeCaptureDevices, new AudioDeviceComparer());
            foreach (var device in activeDevices)
            {
                if(device.DeviceID != config.SelectedSpeakerDeviceId) { continue; }
                try
                {
                    using (var meter = AudioMeterInformation.FromDevice(device))
                    {
                        vols.Add(meter.GetPeakValue() * (float)config.SpeakerCaptureVolumeMultiplier);
                    }
                }
                catch
                {
                    vols.Add(0f);
                }
            }

            vols.Add(0f);

            if (config.IsCaptureMicrophone)
            {
                foreach (var device in activeCaptureDevices)
                {
                    if (device.DeviceID != config.SelectedMicrophoneDeviceId) { continue; }
                    try
                    {
                        using (var meter = AudioMeterInformation.FromDevice(device))
                        {
                            vols.Add(meter.GetPeakValue() * (float)config.MicrophoneCaptureVolumeMultiplier);
                        }
                    }
                    catch
                    {
                        vols.Add(0f);
                    }
                }
                vols.Add(0f);
            }

            __result = vols.Max();

            return false;
        }

        public void ShowSettingWindow()
        {
            if (settingWindow == null)
            {
                settingWindow = new SettingWindow(this);
                settingWindow.Show();
                settingWindow.Topmost = true;
            }
            else
            {
                settingWindow.Topmost = true;
            }
        }

        public void ShowDebugWindow()
        {
            if (debugWindow == null)
            {
                debugWindow = new DebugWindow(this);
                debugWindow.Show();
                debugWindow.Topmost = true;
            }
            else
            {
                debugWindow.Topmost = true;
            }
        }
    }

    [Serializable]
    public class Config
    {
        private bool isCaptureMicrophone = false;
        private double speakerCaptureVolumeMultiplier = 1f;
        private double microphoneCaptureVolumeMultiplier = 1f;
        private string selectedSpeakerDeviceId = "";
        private string selectedMicrophoneDeviceId = "";

        public bool IsCaptureMicrophone
        {
            get { return isCaptureMicrophone; }
            set { isCaptureMicrophone = value; }
        }

        public double SpeakerCaptureVolumeMultiplier
        {
            get { return speakerCaptureVolumeMultiplier; }
            set { speakerCaptureVolumeMultiplier = value; }
        }

        public double MicrophoneCaptureVolumeMultiplier
        {
            get { return microphoneCaptureVolumeMultiplier; }
            set { microphoneCaptureVolumeMultiplier = value; }
        }

        public string SelectedSpeakerDeviceId
        {
            get { return selectedSpeakerDeviceId; }
            set { selectedSpeakerDeviceId = value; }
        }

        public string SelectedMicrophoneDeviceId
        {
            get { return selectedMicrophoneDeviceId; }
            set { selectedMicrophoneDeviceId = value; }
        }

        private static JsonSerializerSettings jsonSerializerSettings = null;

        public static JsonSerializerSettings JsonSerializerSettings
        {
            get
            {
                if (jsonSerializerSettings == null)
                {
                    jsonSerializerSettings = new JsonSerializerSettings();
                    jsonSerializerSettings.Formatting = Formatting.Indented;
                    jsonSerializerSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
                    jsonSerializerSettings.DefaultValueHandling = DefaultValueHandling.Include;
                    JsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                }
                return jsonSerializerSettings;
            }
        }

        public static Config ReadConfig(string pluginDirectory) 
        {
            try
            {
                if (!File.Exists(pluginDirectory + "config.json") || File.ReadAllText(pluginDirectory + "config.json") == "")
                {
                    File.WriteAllText(pluginDirectory + "config.json", JsonConvert.SerializeObject(new Config(), JsonSerializerSettings));
                    return new Config();
                }
                Config temp = JsonConvert.DeserializeObject<Config>(File.ReadAllText(pluginDirectory + "config.json"));
                if(temp == null) temp = new Config();
                if (!MultiChannelMusicFix.activeRenderDevices.Except(MultiChannelMusicFix.activeCaptureDevices, new AudioDeviceComparer()).Any((device) => { return device.DeviceID == temp.selectedSpeakerDeviceId; }))
                {
                    temp.selectedSpeakerDeviceId = "";
                }
                if (!MultiChannelMusicFix.activeCaptureDevices.Any((device) => { return device.DeviceID == temp.selectedMicrophoneDeviceId; }))
                {
                    temp.selectedMicrophoneDeviceId = "";
                }
                return temp;
            }
            catch (JsonException e)
            {
                MessageBox.Show(string.Format("配置文件解析失败，将还原到默认值\n错误信息: \n{0}", e.Message + "\n" + e.StackTrace), "VPet.Plugin.MultiChannelMusicFix", MessageBoxButton.OK);
                return new Config();
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("配置文件读取失败，将还原到默认值\n错误信息: \n{0}", e.Message + "\n" + e.StackTrace), "VPet.Plugin.MultiChannelMusicFix", MessageBoxButton.OK);
                return new Config();
            }
        }

        public void SaveConfig(string pluginDirectory) 
        {
            try
            {
                File.WriteAllText(pluginDirectory + "config.json", JsonConvert.SerializeObject(this, JsonSerializerSettings));
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("配置文件保存失败，请检查程序写入权限\n错误信息: \n{0}", e.Message + "\n" + e.StackTrace), "VPet.Plugin.MultiChannelMusicFix", MessageBoxButton.OK);
            }
        }
    }

    public class AudioDeviceComparer : IEqualityComparer<MMDevice> 
    {
        public bool Equals(MMDevice a, MMDevice b) 
        {
            return a.FriendlyName == b.FriendlyName;
        }

        public int GetHashCode(MMDevice device) 
        {
            return device.FriendlyName.GetHashCode();
        }
    }
}