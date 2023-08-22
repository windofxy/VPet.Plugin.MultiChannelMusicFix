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

namespace VPet.Plugin.MultiChannelMusicFix
{
    public class MultiChannelMusicFix : MainPlugin
    {
        public override string PluginName => "多声道设备音乐识别修复&麦克风音乐检测 Multi Channel Music Fix & Microphone Music Capture";

        public IMainWindow mainWindow;
        public SettingWindow settingWindow = null;
        public DebugWindow debugWindow = null;

        public Assembly[] assemblies;
        public Assembly targetAssembly;
        public Type[] types;
        public Type targetType;
        public MethodInfo targetMethod;
        public MethodOperation redirectedMethod;

        public static string pluginDirectory;
        public static Config config = new Config();

        public MultiChannelMusicFix(IMainWindow _MainWindow) : base(_MainWindow)
        {
            mainWindow = _MainWindow;
        }

        public override void LoadPlugin()
        {
            SetupUI();
            
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
            redirectedMethod = CustomReflection.RedirectTo(targetMethod, typeof(MultiChannelMusicFix).GetMethod("AudioPlayingVolume"));
            //volume = (Single)targetMethod.Invoke(mainWindow, null);
            //ShowDebugWindow();
        }

        public override void Setting()
        {
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

        public float AudioPlayingVolume()
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                if (!MultiChannelMusicFix.config.isCaptureMicrophone)
                {
                    using (var activeRenderDevices = enumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active))
                    {
                        using (var activeCaptureDevices = enumerator.EnumAudioEndpoints(DataFlow.Capture, DeviceState.Active))
                        {
                            List<float> vols = new List<float>();
                            var activeDevices = activeRenderDevices.Except(activeCaptureDevices, new AudioDeviceComparer());
                            foreach (var device in activeDevices)
                            {
                                using (var meter = AudioMeterInformation.FromDevice(device))
                                {
                                    vols.Add(meter.GetPeakValue());
                                }
                            }
                            return vols.Max();
                        }
                    }
                }
                else 
                {
                    using (var activeDevices = enumerator.EnumAudioEndpoints(DataFlow.All, DeviceState.Active))
                    {
                        List<float> vols = new List<float>();
                        foreach (var device in activeDevices)
                        {
                            using (var meter = AudioMeterInformation.FromDevice(device))
                            {
                                vols.Add(meter.GetPeakValue());
                            }
                        }
                        return vols.Max();
                    }
                }
            }
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

    public static class CustomReflection
    {
        public static MethodOperation RedirectTo<T>(this MethodInfo origin, Func<T> target)
                => RedirectTo(origin, target.Method);

        public static MethodOperation RedirectTo<T, R>(this MethodInfo origin, Func<T, R> target)
                => RedirectTo(origin, target.Method);

        public static MethodOperation RedirectTo(this MethodInfo origin, MethodInfo target)
        {
            IntPtr ori = GetMethodAddress(origin);
            IntPtr tar = GetMethodAddress(target);

            Debug.Assert(Marshal.ReadIntPtr(ori) == origin.MethodHandle.GetFunctionPointer());
            Debug.Assert(Marshal.ReadIntPtr(tar) == target.MethodHandle.GetFunctionPointer());

            return Redirect(ori, tar);
        }

        private static void OutputMethodDetails(MethodInfo mi, IntPtr address)
        {
            IntPtr mt = mi.DeclaringType.TypeHandle.Value;
            IntPtr md = mi.MethodHandle.Value;

            int offset = (int)((long)mt - (long)md);

            if (mi.IsVirtual)
            {
                offset = IntPtr.Size == 4 ? 0x28 : 0x40;

                IntPtr ms = mt + offset;

                long shift = Marshal.ReadInt64(md) >> 32;
                ushort mask = 0xffff;
                int slot = (int)(shift & mask);
            }

            offset = (int)((long)address - (long)mt);
        }

        private static IntPtr GetMethodAddress(MethodInfo mi)
        {
            const ushort SLOT_NUMBER_MASK = 0xffff;
            const int MT_OFFSET_32BIT = 0x28;
            const int MT_OFFSET_64BIT = 0x40;

            IntPtr address;

            RuntimeHelpers.PrepareMethod(mi.MethodHandle);

            IntPtr md = mi.MethodHandle.Value;
            IntPtr mt = mi.DeclaringType.TypeHandle.Value;

            if (mi.IsVirtual)
            {
                int offset = IntPtr.Size == 4 ? MT_OFFSET_32BIT : MT_OFFSET_64BIT;

                IntPtr ms = Marshal.ReadIntPtr(mt + offset);

                long shift = Marshal.ReadInt64(md) >> 32;
                int slot = (int)(shift & SLOT_NUMBER_MASK);

                address = ms + (slot * IntPtr.Size);
            }
            else
            {
                address = md + 8;
            }

            return address;
        }

        private static MethodRedirection Redirect(IntPtr ori, IntPtr tar)
        {
            var token = new MethodRedirection(ori);
            Marshal.Copy(new IntPtr[] { Marshal.ReadIntPtr(tar) }, 0, ori, 1);

            return token;
        }
    }
    public abstract class MethodOperation : IDisposable
    {
        public abstract void Restore();

        public void Dispose()
        {
            Restore();
        }
    }

    public class MethodRedirection : MethodOperation
    {
        public MethodToken Origin { get; private set; }

        public MethodRedirection(IntPtr address)
        {
            Origin = new MethodToken(address);
        }

        public override void Restore()
        {
            Origin.Restore();
        }

        public override string ToString()
        {
            return Origin.ToString();
        }
    }
    public struct MethodToken : IDisposable
    {
        public IntPtr Address { get; private set; }
        public IntPtr Value { get; private set; }

        public MethodToken(IntPtr address)
        {
            Address = address;
            Value = Marshal.ReadIntPtr(address);
        }

        public void Restore()
        {
            Marshal.Copy(new IntPtr[] { Value }, 0, Address, 1);
        }

        public void Dispose()
        {
            Restore();
        }
    }
}