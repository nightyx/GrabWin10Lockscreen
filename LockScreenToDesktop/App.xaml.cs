using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;

namespace LockScreenToDesktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        // Have watcher here to be able to exit it.
        ManagementEventWatcher watcher;

        // Main Method of the application. 
        // Create Icon and start listener for registry changes here
        public void Application_Startup(object sender, StartupEventArgs e)
        {
            TaskbarIcon tbi = new TaskbarIcon();
            tbi.Icon = LockScreenToDesktop.Properties.Resources.Yoshi;
            tbi.ToolTipText = "Win10 Lock Screen to Wallpaper";

            Listener();

            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            registryKey.SetValue("LockScreenToDesktop", System.AppDomain.CurrentDomain.BaseDirectory + "LockScreenToDesktop.exe");

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        
        }

        void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            // Stop listening for events.
            watcher.Stop();
        }


        // This method creates a listener for the change of the Lock Screen path
        public void Listener()
        {
            // Get User Identity, since WqlEventQuery can't access hkey_current_user
            // http://www.codeproject.com/Articles/30624/Asynchronous-Registry-Notification-Using-Strongly?msg=2844468#xx2844468xx
            WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
            
            // FROM: http://stackoverflow.com/questions/826971/registry-watcher-c-sharp
            // NOT WORKING WITH HKEY_CURRENT_USER
            WqlEventQuery query = new WqlEventQuery(
                     "SELECT * FROM RegistryValueChangeEvent WHERE " +
                     "Hive = 'HKEY_USERS'" +
                     @" AND KeyPath = '" + currentUser.User.Value + @"\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Lock Screen\\Creative' AND ValueName='LandscapeAssetPath'");
            // Last part is likely dynamic...how to get it?     

            watcher = new ManagementEventWatcher(query);

            // Set up the delegate that will handle the change event.
            watcher.EventArrived += new EventArrivedEventHandler(HandleEvent);

            // Start listening for events.
            watcher.Start();
        }

        // Event handler: saves picture to My Pictures and sets it as a wallpaper
        private void HandleEvent(object sender, EventArrivedEventArgs e)
        {
            // Get Path of LockScreen File
            WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
            string assetPath = Registry.Users.OpenSubKey(currentUser.User.Value + "\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Lock Screen\\Creative").GetValue("LandscapeAssetPath").ToString();

            // Get file name
            int pos = assetPath.LastIndexOf("\\") + 1;
            string filename = assetPath.Substring(pos, assetPath.Length - pos);

            // Prepend Date
            filename = DateTime.Today.ToString("yyyy.MM.dd") + " " + filename;

            string picturePath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string fullpicpath = picturePath + "\\" + filename + ".jpg";

            assetPath = assetPath.Replace("/", "//");
            fullpicpath = fullpicpath.Replace("/", "//");

            File.Copy(assetPath, fullpicpath, true);

            SetDesktopBackground(fullpicpath);
        }

        // Method to set wallpaper
        private void SetDesktopBackground(string pathToImage)
        {
            System.Drawing.Image img = System.Drawing.Image.FromFile(pathToImage);

            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            key.SetValue(@"WallpaperStyle", 10.ToString());
            key.SetValue(@"TileWallpaper", 0.ToString());

            SystemParametersInfo(SPI_SETDESKWALLPAPER,
                0,
                pathToImage,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

    }
}
