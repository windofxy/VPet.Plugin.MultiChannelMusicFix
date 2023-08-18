using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Reflection;
using VPet_Simulator.Windows.Interface;
using VPet_Simulator.Core;
using System.Runtime.CompilerServices;
using VPet_Simulator.Windows;
using System.Windows.Controls;
using System.Security.RightsManagement;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection.Emit;
using CSCore.CoreAudioAPI;

namespace VPet.Plugin.MultiChannelMusicFix
{
    public class AudioDeviceSelector : MainPlugin
    {
        public override string PluginName => "AudioDeviceSelector";

        public DebugWindow settingWindow = null;

        public Assembly[] assemblies;
        public Assembly targetAssembly;
        public Type[] types;
        public Type targetType;
        public MethodInfo targetMethod;
        public Single volume;
        public MethodOperation result;
        public IMainWindow mainWindow;

        public AudioDeviceSelector(IMainWindow _MainWindow) : base(_MainWindow)
        {
            mainWindow = _MainWindow;
        }

        public override async void LoadPlugin()
        {
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
            result = CustomReflection.RedirectTo(targetMethod, typeof(AudioDeviceSelector).GetMethod("AudioPlayingVolume"));
            //volume = (Single)targetMethod.Invoke(mainWindow, null);
            //ShowDebugWindow();
        }

        private bool? AudioPlayingVolumeOK = null;
        public float AudioPlayingVolume()
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                using (var activeDevices = enumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active))
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

        public void ShowDebugWindow()
        {
            if (settingWindow == null)
            {
                settingWindow = new DebugWindow(this);
                settingWindow.Show();
            }
            else
            {
                settingWindow.Topmost = true;
            }
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