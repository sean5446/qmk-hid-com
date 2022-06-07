
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Text;
using System.Management;
using Microsoft.VisualBasic;

namespace HidCom
{
    class Program
    {
        // these must match the hid firmware
        private const int vendorId = 0xBABA;
        private static readonly int[] productIds = new[] { 0x6060 };
        private static HidDevice device;
        private static bool attached;

        // performance stats
        private static PerformanceCounter cpuCounter;
        private static Microsoft.VisualBasic.Devices.ComputerInfo computerInfo;

        private const int pollTime = 50;
        private static readonly byte[] junkByte = new byte[] { 0x66 };

        private static string screenText;

        static void Main()
        {
            Console.WriteLine("Getting system stat resources...");
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            computerInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();

            Console.WriteLine("Looking for device...");
            // find device
            List <HidDevice> devices = new List<HidDevice>();
            foreach (var dev in HidDevices.Enumerate(vendorId, productIds))
            {
                devices.Add(dev);
                Console.WriteLine(dev);
            }
            //Console.ReadLine();

            // don't know why there are 3 devices, but index 1 works
            device = devices[1];
            device.OpenDevice();
            device.Inserted += DeviceAttachedHandler;
            device.Removed += DeviceRemovedHandler;
            device.MonitorDeviceEvents = true;
            device.ReadReport(OnReport);

            if (device != null)
            {
                Console.WriteLine("Device found, press any key to exit");
                Console.ReadKey();
                device.CloseDevice();
            }
            else
            {
                Console.WriteLine("Could not find a device");
                Console.ReadKey();
            }
        }

        private static void OnReport(HidReport report)
        {
            if (attached == false) { return; }

            if (report.Data.Length > 0)
            {
                Console.WriteLine("{0}", BitConverter.ToString(report.Data));
                Console.WriteLine("got:       " + report.Data[0]);

                if (report.Data[0] == 0)
                {
                    screenText = GetScreenText();
                    Console.WriteLine("generating " + screenText);
                }
                if (report.Data[0] < 4 && screenText != null)
                {
                    string send = " " + screenText.Substring(report.Data[0] * 21, 21);
                    Console.WriteLine("sent       '" + send + "' " + send.Length);

                    byte[] sendBytes = Encoding.ASCII.GetBytes(send);
                    device.Write(sendBytes);
                }
                else
                {
                    // send junk to keep coms going
                    device.WriteAsync(junkByte);
                    Console.WriteLine("sent       junk");

                    if (report.Data[0] == 4) {
                        Console.WriteLine("sleeping...");
                        Thread.Sleep(1000);
                    }
                }

                Thread.Sleep(pollTime);
            }

            device.ReadReport(OnReport);
        }

        private static void DeviceAttachedHandler()
        {
            attached = true;
            Console.WriteLine("Device attached.");
            // send something to get communication started
            device.Write(junkByte);
            device.ReadReport(OnReport);
        }

        private static void DeviceRemovedHandler()
        {
            attached = false;
            Console.WriteLine("Device removed.");
        }


        private static ulong GetUsedMemory()
        {
            ulong available = computerInfo.AvailablePhysicalMemory;
            ulong total = computerInfo.TotalPhysicalMemory;
            return total - available;
        }

        private static string GetScreenText()
        {
            // 21 characters per line, 4 rows of text

            //screen = new string(Enumerable.Repeat(chars, 84).Select(s => s[random.Next(s.Length)]).ToArray());

            string cpu = Math.Round(cpuCounter.NextValue(), 2) + "%";
            string mem = Math.Round(GetUsedMemory() / (1024.0 * 1024 * 1024), 2) + "GB";

            cpu = string.Format("CPU : {0}", cpu.PadLeft(15));
            mem = string.Format("MEM : {0}", mem.PadLeft(15));

            string 
            screen =  cpu;
            screen += mem;
            screen += "1234567890_1234567890";
            screen += "1234567890_1234567890";

            return screen;
        }
    }
}
