using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Management.Instrumentation;
using System.IO;
using System.Xml.Serialization;
using System.Threading;
using Microsoft.Win32;

namespace GCI_Data_Usage
{
    public partial class Form1 : Form
    {
        Icon[] iconArray = new Icon[101];
        NotifyIcon activeIcon;
        Thread gciWorker;
        private int loginErrorCounter = 0;
        private string internetUsageCap;
        private string internetUsageTotal;
        private string wirelessUsageCap;
        private string wirelessUsageTotal;
        private int internetPrecentage = Int32.Parse(Settings1.Default["usage"].ToString());

        public Form1()
        {
            #region Initialization Data (username and password)
            InitializeComponent();
            textBox_Username.Text = Settings1.Default["username"].ToString();
            textBox_Password.Text = Settings1.Default["password"].ToString();
            comboBox1.Text = Settings1.Default["frequency"].ToString();
            comboBox2.Text = Settings1.Default["theme"].ToString();


            //set icons objects
            buildIconArray(Settings1.Default["theme"].ToString());

            //create notify icon and assign idle icon and show it
            activeIcon = new NotifyIcon();
            activeIcon.Icon = iconArray[Int32.Parse(Settings1.Default["usage"].ToString())];
            activeIcon.Visible = true;

            //create menu items
            MenuItem programNameMenuItem = new MenuItem("GCI internet usage BETA v1.1.2");
            MenuItem programNameMenuItem2 = new MenuItem("Check for updates");
            MenuItem quitMenuItem = new MenuItem("Quit");
            ContextMenu contextMenu = new ContextMenu();  //create the contect menu
            contextMenu.MenuItems.Add(programNameMenuItem);  //add items to the context menu
            contextMenu.MenuItems.Add(programNameMenuItem2);  //add items to the context menu
            contextMenu.MenuItems.Add(quitMenuItem);
            activeIcon.ContextMenu = contextMenu;  //link context menu to the tray icon
            //set initial icon hover text.
            if(System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                activeIcon.Text = "Connected! Loading";
            } else {
                activeIcon.Text = "Waiting for internet";
            }


            //Make the first menu item unclickable
            contextMenu.MenuItems[0].Enabled = false;

            //click event for icon to open options
            activeIcon.Click += activeIcon_Click;

            //wire up quit button to close application
            quitMenuItem.Click += QuitMenuItem_Click;

            //wire up quit button to close application
            programNameMenuItem2.Click += programNameMenuItem2_Click;

            //Hide the form because we don't need it yet
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            //start worker thread that polls GCI
            gciWorker = new Thread(new ThreadStart(GCIActivityThread));
            gciWorker.Start();

            //adds an additional "on complete" task for the webbrowser
            webBrowser1.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser1_DocumentCompleted);
            webBrowser1.Navigate("https://login.gci.com");
            #endregion
        }

        public void buildIconArray(string name)
        {
            for (int i = 0; i <= 100; i++)
            {
                iconArray[i] = new Icon(Application.StartupPath.ToString()+"/icons/" + name + "/" + i + ".ico");
            }
        }

        private void activeIcon_Click(object sender, EventArgs e)
        {
            this.Activate();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void QuitMenuItem_Click(object sender, EventArgs e)
        {
            gciWorker.Abort();
            activeIcon.Dispose();
            this.Close();
        }

        private void programNameMenuItem2_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.allthedurbin.com/gci-usage-app/");
        }

        public void GCIActivityThread()
        {
            //wbemtest command to add u/d telemetrics later
            //main loop
            while (true)
            {
                int internetError = 0;
                while(!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    if(internetError == 0)
                    {
                        activeIcon.Text = "Waiting for internet";
                        internetError = 1;
                    }
                    
                    Thread.Sleep(60000);
                }
                webBrowser1.Navigate("https://apps.gci.com/um/overview");
                Thread.Sleep(1000 * 60 * 60 * Int32.Parse(Settings1.Default["frequency"].ToString()));
            }
        }

        void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (webBrowser1.Document.Url.Equals("https://login.gci.com"))
            {
                progressBar1.Value = 25;
                if (loginErrorCounter < 2)
                {
                    webBrowser1.Document.GetElementById("username").SetAttribute("value", Settings1.Default["username"].ToString());
                    webBrowser1.Document.GetElementById("password").SetAttribute("value", Settings1.Default["password"].ToString());
                    webBrowser1.Document.GetElementById("login").InvokeMember("click");
                    loginErrorCounter += 1;
                }
                else
                {
                    //This will display the login page if it cannot log in, no matter what.
                    errorStop("Unable To Login, Check your username and password then click save");
                }
            }

            if (webBrowser1.Document.Url.Equals("https://www.gci.com/my-gci"))
            {
                progressBar1.Value = 50;
                webBrowser1.Navigate("https://apps.gci.com/um/overview");
            }

            if (webBrowser1.Document.Url.Equals("https://apps.gci.com/um/overview"))
            {
                progressBar1.Value = 75;
                //uses javascript to get the value of the internet cap, including extra buckets.
                internetUsageCap = webBrowser1.Document.InvokeScript("eval", new[] {
    "(function() { var internetCap=0;$.each($('#internet .cap'), function(index, value){internetCap=internetCap + parseInt(value.innerHTML);});return String(internetCap); })()" }).ToString();
                internetUsageTotal = webBrowser1.Document.InvokeScript("eval", new[] {
    "(function() { var internetTotal=0;$.each($('#internet .total'), function(index, value){internetTotal=internetTotal + parseFloat(value.innerHTML);});return String(internetTotal);})()" }).ToString();

                wirelessUsageCap = webBrowser1.Document.InvokeScript("eval", new[] {
    "(function() { var wirelessCap=0;$.each($('#shared_mobile .cap'), function(index, value){wirelessCap=wirelessCap + parseFloat(value.innerHTML);return false;});return String(wirelessCap);})()" }).ToString();
                wirelessUsageTotal = webBrowser1.Document.InvokeScript("eval", new[] {
    "(function() { var wirelessTotal=0;$.each($('#shared_mobile .total'), function(index, value){wirelessTotal=wirelessTotal + parseFloat(value.innerHTML);return false;});return String(wirelessTotal);})()" }).ToString();

                int internetPrecentage = Convert.ToInt32(Math.Round(Convert.ToDouble(internetUsageTotal))) * 100 / (Int32.Parse(internetUsageCap)) ;
                activeIcon.Icon = iconArray[internetPrecentage];
                Settings1.Default["usage"] = Int32.Parse(internetPrecentage.ToString());
                Settings1.Default.Save();
                activeIcon.Text = "Home Internet: "+internetUsageTotal.ToString()+"GB /"+ internetUsageCap.ToString()+" GB";
                if(wirelessUsageCap.ToString() != "0")
                {
                    activeIcon.Text += "\n";
                    activeIcon.Text += "Wireless: "+wirelessUsageTotal.ToString() + "GB /" + wirelessUsageCap.ToString() + " GB";
                }
                
                progressBar1.Value = 100;
                Thread.Sleep(100);
                progressBar1.Value = 0;
            }
        }

        private void errorStop(string message)
        {
            //bring window to front, unminimize, focus
            MessageBox.Show(message);
            progressBar1.Value = 0;
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        //override the close button to ask if you want to close. most people want to minimze
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.CloseReason == CloseReason.WindowsShutDown) return;

            // Confirm user wants to close
            switch (MessageBox.Show(this, "Are you sure you want to close?", "Minimizing", MessageBoxButtons.YesNo))
            {
                case DialogResult.No:
                    e.Cancel = true;
                    break;
                default:
                    activeIcon.Dispose();
                    gciWorker.Abort();
                    break;
            }
        }

        //override default minimze action to hide the app again
        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
        }

        //save button clicked
        private void button1_Click_1(object sender, EventArgs e)
        {
            Settings1.Default["username"] = textBox_Username.Text.ToString();
            Settings1.Default["password"] = textBox_Password.Text.ToString();
            Settings1.Default.Save();
            Application.Restart();
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(activeIcon != null)
            {
               Settings1.Default["theme"] = comboBox2.Text.ToString();
               buildIconArray(comboBox2.Text.ToString());
               activeIcon.Icon = iconArray[internetPrecentage];

            } 
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings1.Default["frequency"] = Int32.Parse(comboBox1.Text.ToString());
        }
    }
}
