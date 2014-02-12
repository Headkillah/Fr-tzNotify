using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

using System.Diagnostics;

namespace Fr_tzNotify
{
    public class SysTrayApp : Form
    {
        [STAThread]
        public static void Main()
        {
            Application.Run(new SysTrayApp());
        }

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private FritzHosts hostList = null;

        public SysTrayApp()
        {
            // create icon
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Fri!tzNotify";
            trayIcon.Icon = new Icon(Properties.Resources.IconOn,16,16);

            // create context menu
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Lade");
            (trayMenu.Items[0] as ToolStripMenuItem).Enabled = false;
            //(trayMenu.Items[0] as ToolStripMenuItem).Icon = Cursors.WaitCursor;
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Aktualisieren", null, OnUpdate);
            trayMenu.Items.Add("Beenden", null, OnExit);

            // add menu and set icon visible
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible     = true;

            // initialize list of hosts
            UpdateHosts();

            // set timer for updates
            Timer updateTimer = new Timer();
            updateTimer.Tick += new EventHandler(OnUpdate);
            updateTimer.Interval = 2000;
            updateTimer.Start();
        }

        private void UpdateHosts()
        {
            WebClient wC = new WebClient();

            wC.DownloadStringCompleted += (wCsender, wCe) =>
            {
                try
                {
                    string json = wCe.Result;

                    FritzHosts hosts = JsonConvert.DeserializeObject<FritzHosts>(json);

                    if (this.hostList != null)
                    {
                        // clear all devices in menu
                        (trayMenu.Items[0] as ToolStripMenuItem).Enabled = true;
                        (trayMenu.Items[0] as ToolStripMenuItem).Text = "Geräte";
                        (trayMenu.Items[0] as ToolStripMenuItem).DropDownItems.Clear();

                        foreach (FritzHostEntry curr in hosts.hosts)
                        {
                            if (curr.isActive())
                            {
                                ToolStripMenuItem device = new ToolStripMenuItem();
                                device.Text = curr.name;

                                if (curr.getType() == FritzHostEntry.connectionType.WIFI)
                                    device.Image = new Icon(Properties.Resources.IconWiFi, 16, 16).ToBitmap();
                                else
                                    device.Image = new Icon(Properties.Resources.IconEthernet, 16, 16).ToBitmap();

                                device.Click += (sender, e) => { DeviceClicked(sender, e, curr); };

                                (trayMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add(device);
                            }

                            foreach (FritzHostEntry prev in this.hostList.hosts)
                            {
                                if (prev.mac != curr.mac)
                                    continue;

                                if (prev.isActive() != curr.isActive())
                                {
                                    this.ShowNotification(curr.name, curr.isActive());
                                }
                            }
                        }
                    }

                    this.hostList = hosts;
                    trayIcon.Icon = new Icon(Properties.Resources.IconOn, 16, 16);
                }
                catch (Exception)
                {
                    (trayMenu.Items[0] as ToolStripMenuItem).Enabled = false;
                    (trayMenu.Items[0] as ToolStripMenuItem).Text = "Offline";
                    trayIcon.Icon = new Icon(Properties.Resources.IconOff, 16, 16);
                }
            };

            wC.DownloadStringAsync( new Uri("http://fritz.box/query.lua?hosts=landevice:settings/landevice/list(name,ip,active,mac,wlan,ethernet,speed)") );
        }

        private void DeviceClicked(object sender, EventArgs e, FritzHostEntry host)
        {
            Process.Start(@"\\"+host.ip);
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            base.OnLoad(e);
        }

        private void ShowNotification( string name, bool status )
        {
            trayIcon.ShowBalloonTip(500, name, ((status) ? "Hat sich angemeldet" : "Hat sich abgemeldet"), ToolTipIcon.None);
        }

        private void OnUpdate(object sender, EventArgs e)
        {
            this.UpdateHosts();
        }

        private void OnExit(object sender, EventArgs e)
        {
            Application.Exit();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                trayIcon.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }

    public class FritzHosts
    {
        public List<FritzHostEntry> hosts { get; set; }
    }

    public class FritzHostEntry
    {
        public string name { get; set; }
        public string ip { get; set; }
        public string mac { get; set; }
        public string active { get { throw new Exception(); } set { this._active = value == "1" ? true : false; } }
        public string wlan { get { throw new Exception(); } set { this._wlan = value == "1" ? true : false; } }
        public string ethernet { get { throw new Exception(); } set { this._ethernet = value == "1" ? true : false; } }
        public string speed { get { throw new Exception(); } set { this._speed = Convert.ToInt16(value); } }

        private bool _active { get; set; }
        private bool _wlan { get; set; }
        private bool _ethernet { get; set; }
        private int _speed { get; set; }

        public enum connectionType { WIFI, ETHERNET }

        public bool isActive()
        {
            return this._speed > 0 && this._active ? true : false;
        }

        public connectionType getType()
        {
            return this._wlan ? connectionType.WIFI : connectionType.ETHERNET;
        }

        public int getSpeed()
        {
            return this._speed;
        }
    }
}
