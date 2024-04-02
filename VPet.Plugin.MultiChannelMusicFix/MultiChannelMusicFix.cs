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
        public static MMDeviceCollection activeAllDevices = null;
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

            //ShowDebugWindow();
        }

        public override void Setting()
        {
            UpdateAudioDevicesList();
            ShowSettingWindow();
        }

        public void SetupUI() 
        {
            var menuMODConfig = mainWindow.Main.ToolBar.MenuMODConfig;
            menuMODConfig.Visibility = System.Windows.Visibility.Visible;
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
                activeAllDevices = deviceEnumerator.EnumAudioEndpoints(DataFlow.All, DeviceState.Active);
                activeRenderDevices = deviceEnumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active);
                activeCaptureDevices = deviceEnumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active);
            }
            finally { }
        }

        [HarmonyPrefix]
        [HarmonyPatch("AudioPlayingVolume")]
        public static bool AudioPlayingVolumePatch(ref float __result)
        {
            if (!MultiChannelMusicFix.config.isCaptureMicrophone)
            {
                List<float> vols = new List<float>();
                var activeDevices = MultiChannelMusicFix.activeRenderDevices.Except(MultiChannelMusicFix.activeCaptureDevices, new AudioDeviceComparer());
                foreach (var device in activeDevices)
                {
                    try
                    {
                        using (var meter = AudioMeterInformation.FromDevice(device))
                        {
                            vols.Add(meter.GetPeakValue());
                        }
                    }
                    catch
                    {
                        vols.Add(0f);
                    }
                }
                vols.Add(0f);
                __result = vols.Max();
            }
            else
            {
                List<float> vols = new List<float>();
                foreach (var device in MultiChannelMusicFix.activeAllDevices)
                {
                    try
                    {
                        using (var meter = AudioMeterInformation.FromDevice(device))
                        {
                            vols.Add(meter.GetPeakValue());
                        }
                    }
                    catch
                    {
                        vols.Add(0f);
                    }
                }
                vols.Add(0f);
                __result = vols.Max();
            }

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

    public class Config
    {
        public bool isCaptureMicrophone;

        public Config() 
        {
            isCaptureMicrophone = false;
        }

        public static Config ReadConfig(string pluginDirectory) 
        {
            JsonSerializer serializer = new JsonSerializer();
            if (!File.Exists(pluginDirectory + "config.json") || File.ReadAllText(pluginDirectory + "config.json") == "")
            {
                StringWriter stringWriter = new StringWriter();
                serializer.Serialize(new JsonTextWriter(stringWriter), new Config());
                File.WriteAllText(pluginDirectory + "config.json", stringWriter.GetStringBuilder().ToString());
                return new Config();
            }
            JsonReader jsonReader = new JsonTextReader(new StringReader(File.ReadAllText(pluginDirectory + "config.json")));
            return serializer.Deserialize<Config>(jsonReader);
        }

        public void SaveConfig(string pluginDirectory) 
        {
            JsonSerializer serializer = new JsonSerializer();
            StringWriter stringWriter = new StringWriter();
            serializer.Serialize(new JsonTextWriter(stringWriter), this);
            File.WriteAllText(pluginDirectory + "config.json", stringWriter.GetStringBuilder().ToString());
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