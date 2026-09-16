// DragIn1-Setup.exe
// Single-file, per-user installer for DragIn1. No admin rights, no toolchain,
// no dependencies, no console window. DragIn1.exe is embedded as a resource.
//
// Usage:
//   DragIn1-Setup.exe              show the installer
//   DragIn1-Setup.exe /silent      install with defaults, no window
//   DragIn1-Setup.exe /uninstall   uninstall (what Add/Remove Programs calls)

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DragIn1Setup
{
    public static class Info
    {
        public const string AppName = "DragIn1";
        public const string Version = "1.0.0";
        public const string Publisher = "WildTech Development";
        public const string ExeName = "DragIn1.exe";
        public const string SetupName = "DragIn1-Setup.exe";
        public const string RegUninstall =
            "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\DragIn1";
        public const string RegRun =
            "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

        static string installDir;   // chosen on the first page

        public static string DefaultInstallDir
        {
            get
            {
                return Path.Combine(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Programs"),
                    AppName);
            }
        }

        public static string InstallDir
        {
            get { return string.IsNullOrEmpty(installDir) ? DefaultInstallDir : installDir; }
            set { installDir = value; }
        }

        public static string ExePath { get { return Path.Combine(InstallDir, ExeName); } }
        public static string SetupPath { get { return Path.Combine(InstallDir, SetupName); } }

        public static string StartMenuLnk
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    AppName + ".lnk");
            }
        }

        public static string DesktopLnk
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    AppName + ".lnk");
            }
        }

        public static string DataDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppName);
            }
        }

        /// <summary>Where a previous install put itself, if there is one.</summary>
        public static string ExistingInstallDir()
        {
            try
            {
                RegistryKey k = Registry.CurrentUser.OpenSubKey(RegUninstall);
                if (k != null)
                {
                    object v = k.GetValue("InstallLocation");
                    k.Close();
                    if (v != null && Directory.Exists(v.ToString())) return v.ToString();
                }
            }
            catch { }
            return null;
        }
    }

    public static class Actions
    {
        public static void StopRunning()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("DragIn1"))
                {
                    try { p.CloseMainWindow(); p.WaitForExit(2000); }
                    catch { }
                    try { if (!p.HasExited) p.Kill(); }
                    catch { }
                }
            }
            catch { }
            System.Threading.Thread.Sleep(400);
        }

        public static void ExtractApp()
        {
            try
            {
                Directory.CreateDirectory(Info.InstallDir);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not create the folder:\r\n" + Info.InstallDir + "\r\n\r\n" +
                    ex.Message + "\r\n\r\n" +
                    "Pick a different location, somewhere under your user folder.");
            }

            // Fail early and clearly if the folder is not writable.
            string probe = Path.Combine(Info.InstallDir, ".write-test");
            try
            {
                File.WriteAllText(probe, "x");
                File.Delete(probe);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "That folder is not writable:\r\n" + Info.InstallDir + "\r\n\r\n" +
                    ex.Message + "\r\n\r\n" +
                    "Folders like Program Files need administrator rights. Choose a " +
                    "location under your user folder instead.");
            }

            Assembly asm = Assembly.GetExecutingAssembly();
            Stream s = asm.GetManifestResourceStream(Info.ExeName);
            if (s == null)
                throw new Exception(
                    "This installer is missing its payload (" + Info.ExeName + ").\r\n" +
                    "Rebuild it with Build-Installer.cmd.");

            using (s)
            using (FileStream fs = new FileStream(Info.ExePath, FileMode.Create, FileAccess.Write))
            {
                byte[] buf = new byte[81920];
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0) fs.Write(buf, 0, n);
            }

            // Verify rather than assume. Antivirus can quarantine a freshly written exe.
            FileInfo fi = new FileInfo(Info.ExePath);
            if (!fi.Exists || fi.Length == 0)
                throw new Exception(
                    "DragIn1.exe was written, then vanished or came out empty:\r\n" +
                    Info.ExePath + "\r\n\r\n" +
                    "That usually means antivirus quarantined it. Allow the file and " +
                    "run setup again.");

            try { File.Copy(Application.ExecutablePath, Info.SetupPath, true); }
            catch { }   // only affects Add/Remove Programs, not the app
        }

        public static void MakeShortcut(string lnkPath, string target, string desc)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            object shell = Activator.CreateInstance(t);
            object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                null, shell, new object[] { lnkPath });
            Type lt = lnk.GetType();
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk,
                new object[] { target });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk,
                new object[] { Path.GetDirectoryName(target) });
            lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk,
                new object[] { target + ",0" });
            lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk,
                new object[] { desc });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }

        public static void RegisterUninstall()
        {
            RegistryKey k = Registry.CurrentUser.CreateSubKey(Info.RegUninstall);
            if (k == null) return;
            long size = 0;
            try { size = new FileInfo(Info.ExePath).Length / 1024; }
            catch { }
            k.SetValue("DisplayName", Info.AppName);
            k.SetValue("DisplayVersion", Info.Version);
            k.SetValue("Publisher", Info.Publisher);
            k.SetValue("InstallLocation", Info.InstallDir);
            k.SetValue("DisplayIcon", Info.ExePath);
            k.SetValue("UninstallString", "\"" + Info.SetupPath + "\" /uninstall");
            k.SetValue("QuietUninstallString", "\"" + Info.SetupPath + "\" /uninstall /silent");
            k.SetValue("EstimatedSize", (int)size, RegistryValueKind.DWord);
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            k.Close();
        }

        public static void SetStartWithWindows(bool on)
        {
            try
            {
                RegistryKey k = Registry.CurrentUser.OpenSubKey(Info.RegRun, true);
                if (k == null) return;
                if (on) k.SetValue(Info.AppName, "\"" + Info.ExePath + "\"");
                else k.DeleteValue(Info.AppName, false);
                k.Close();
            }
            catch { }
        }

        public static void Install(bool startMenu, bool desktop, bool startup)
        {
            StopRunning();
            ExtractApp();

            if (startMenu)
            {
                try { MakeShortcut(Info.StartMenuLnk, Info.ExePath, "DragIn1"); }
                catch { }
            }
            if (desktop)
            {
                try { MakeShortcut(Info.DesktopLnk, Info.ExePath, "DragIn1"); }
                catch { }
            }
            SetStartWithWindows(startup);
            try { RegisterUninstall(); }
            catch { }
        }

        /// <summary>Start the installed app. Throws with a real message on failure.</summary>
        public static void Launch()
        {
            if (!File.Exists(Info.ExePath))
                throw new Exception("DragIn1.exe is not where setup put it:\r\n" + Info.ExePath);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = Info.ExePath;
            psi.WorkingDirectory = Info.InstallDir;
            psi.UseShellExecute = true;
            Process p = Process.Start(psi);
            if (p == null)
                throw new Exception("Windows did not start DragIn1.exe and gave no reason.");
        }

        public static void Uninstall(bool keepFiles)
        {
            StopRunning();
            SetStartWithWindows(false);
            try { File.Delete(Info.StartMenuLnk); }
            catch { }
            try { File.Delete(Info.DesktopLnk); }
            catch { }

            string dir = Info.ExistingInstallDir();
            if (dir != null) Info.InstallDir = dir;

            try { Registry.CurrentUser.DeleteSubKeyTree(Info.RegUninstall, false); }
            catch { }
            if (!keepFiles)
            {
                try { if (Directory.Exists(Info.DataDir)) Directory.Delete(Info.DataDir, true); }
                catch { }
            }
            // The folder holds the running Setup.exe, so it cannot delete itself.
            // Hand that to a detached, hidden cmd that waits for us to exit.
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = "/c ping 127.0.0.1 -n 3 >nul & rd /s /q \"" + Info.InstallDir + "\"";
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
        }
    }

    public class SetupForm : Form
    {
        Panel pageMain, pageDone;
        TextBox txtPath;
        CheckBox cbStartMenu, cbDesktop, cbStartup;
        Button btnInstall, btnClose, btnBrowse, btnLaunch, btnFinish;
        Label lblBusy, lblDonePath;
        bool installed;

        static readonly Color Brand = Color.FromArgb(18, 115, 77);
        static readonly Color Muted = Color.FromArgb(105, 105, 115);

        public SetupForm()
        {
            string existing = Info.ExistingInstallDir();
            installed = existing != null || File.Exists(Info.ExePath);
            if (existing != null) Info.InstallDir = existing;

            Text = "DragIn1 Setup";
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            ClientSize = new Size(560, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            BuildMainPage();
            BuildDonePage();

            Controls.Add(pageMain);
            Controls.Add(pageDone);
            pageDone.Visible = false;
        }

        void BuildMainPage()
        {
            pageMain = new Panel();
            pageMain.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "DragIn1 " + Info.Version;
            title.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(25, 25, 30);
            title.SetBounds(28, 22, 500, 34);

            Label blurb = new Label();
            blurb.Text =
                "Fixes drag and drop from New Outlook, Teams, and web apps.\r\n" +
                "Drop an attachment onto the DragIn1 window, then drag it anywhere you like.";
            blurb.ForeColor = Muted;
            blurb.SetBounds(30, 60, 500, 38);

            Label lblLoc = new Label();
            lblLoc.Text = "Install location";
            lblLoc.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblLoc.SetBounds(30, 112, 200, 18);

            txtPath = new TextBox();
            txtPath.Text = Info.InstallDir;
            txtPath.SetBounds(30, 133, 410, 24);

            btnBrowse = new Button();
            btnBrowse.Text = "Browse...";
            btnBrowse.SetBounds(450, 132, 82, 26);
            btnBrowse.Click += OnBrowse;

            Label note = new Label();
            note.Text = "No administrator rights needed. Installs just for you.";
            note.ForeColor = Muted;
            note.SetBounds(30, 162, 500, 18);

            cbStartMenu = new CheckBox();
            cbStartMenu.Text = "Add to Start Menu";
            cbStartMenu.Checked = true;
            cbStartMenu.SetBounds(30, 196, 480, 24);

            cbDesktop = new CheckBox();
            cbDesktop.Text = "Add a desktop shortcut";
            cbDesktop.SetBounds(30, 224, 480, 24);

            cbStartup = new CheckBox();
            cbStartup.Text = "Start DragIn1 when I sign in";
            cbStartup.Checked = true;
            cbStartup.SetBounds(30, 252, 480, 24);

            lblBusy = new Label();
            lblBusy.SetBounds(30, 300, 500, 40);
            lblBusy.ForeColor = Brand;

            btnInstall = new Button();
            btnInstall.Text = installed ? "Reinstall" : "Install";
            btnInstall.SetBounds(348, 360, 96, 32);
            btnInstall.Click += OnInstall;

            btnClose = new Button();
            btnClose.Text = "Cancel";
            btnClose.SetBounds(452, 360, 80, 32);
            btnClose.Click += delegate { Close(); };

            Button btnUninstall = new Button();
            btnUninstall.Text = "Uninstall";
            btnUninstall.SetBounds(30, 360, 90, 32);
            btnUninstall.Visible = installed;
            btnUninstall.Click += OnUninstall;

            AcceptButton = btnInstall;

            pageMain.Controls.AddRange(new Control[] {
                title, blurb, lblLoc, txtPath, btnBrowse, note,
                cbStartMenu, cbDesktop, cbStartup, lblBusy,
                btnInstall, btnClose, btnUninstall
            });

            if (installed)
                lblBusy.Text = "DragIn1 is already installed. Reinstall to update it.";
        }

        void BuildDonePage()
        {
            pageDone = new Panel();
            pageDone.Dock = DockStyle.Fill;

            Label tick = new Label();
            tick.Text = "✓";
            tick.Font = new Font("Segoe UI", 40F, FontStyle.Bold);
            tick.ForeColor = Brand;
            tick.SetBounds(28, 34, 72, 72);

            Label done = new Label();
            done.Text = "DragIn1 is installed";
            done.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
            done.ForeColor = Color.FromArgb(25, 25, 30);
            done.SetBounds(104, 48, 430, 34);

            lblDonePath = new Label();
            lblDonePath.ForeColor = Muted;
            lblDonePath.AutoEllipsis = true;
            lblDonePath.SetBounds(106, 84, 430, 20);

            Label how = new Label();
            how.Text =
                "How to use it\r\n\r\n" +
                "1.   Open DragIn1. A small window appears, bottom right.\r\n" +
                "2.   Drag an attachment out of New Outlook and drop it on that window.\r\n" +
                "3.   Drag it from there into anything: a folder, a browser upload box, another app.\r\n\r\n" +
                "Tip: select a file in DragIn1 and press Ctrl+C to paste it into an upload dialog instead.";
            how.SetBounds(30, 128, 505, 175);

            btnLaunch = new Button();
            btnLaunch.Text = "Open DragIn1";
            btnLaunch.SetBounds(330, 360, 114, 32);
            btnLaunch.Click += OnLaunchClicked;

            btnFinish = new Button();
            btnFinish.Text = "Finish";
            btnFinish.SetBounds(452, 360, 80, 32);
            btnFinish.Click += delegate { Close(); };

            pageDone.Controls.AddRange(new Control[] {
                tick, done, lblDonePath, how, btnLaunch, btnFinish
            });
        }

        void OnBrowse(object s, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Choose where to install DragIn1";
            dlg.ShowNewFolderButton = true;
            try
            {
                string cur = txtPath.Text.Trim();
                string parent = Path.GetDirectoryName(cur);
                if (Directory.Exists(cur)) dlg.SelectedPath = cur;
                else if (parent != null && Directory.Exists(parent)) dlg.SelectedPath = parent;
            }
            catch { }

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                string p = dlg.SelectedPath;
                // If they picked a generic container, put our own folder inside it.
                if (!string.Equals(Path.GetFileName(p.TrimEnd('\\')), Info.AppName,
                                   StringComparison.OrdinalIgnoreCase))
                {
                    p = Path.Combine(p, Info.AppName);
                }
                txtPath.Text = p;
            }
        }

        void OnInstall(object s, EventArgs e)
        {
            string chosen = txtPath.Text.Trim();
            if (chosen.Length == 0)
            {
                MessageBox.Show(this, "Please choose an install location.", "DragIn1 Setup",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try { chosen = Path.GetFullPath(chosen); }
            catch
            {
                MessageBox.Show(this, "That does not look like a valid folder path.",
                    "DragIn1 Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Info.InstallDir = chosen;

            btnInstall.Enabled = false;
            btnBrowse.Enabled = false;
            lblBusy.ForeColor = Brand;
            lblBusy.Text = "Installing...";
            Cursor = Cursors.WaitCursor;
            Application.DoEvents();

            try
            {
                Actions.Install(cbStartMenu.Checked, cbDesktop.Checked, cbStartup.Checked);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                lblBusy.ForeColor = Color.FromArgb(170, 30, 30);
                lblBusy.Text = "Install failed.";
                btnInstall.Enabled = true;
                btnBrowse.Enabled = true;
                MessageBox.Show(this, ex.Message, "DragIn1 Setup could not finish",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Cursor = Cursors.Default;
            lblDonePath.Text = Info.InstallDir;
            pageMain.Visible = false;
            pageDone.Visible = true;
            AcceptButton = btnLaunch;
            btnLaunch.Focus();
        }

        void OnLaunchClicked(object s, EventArgs e)
        {
            try
            {
                Actions.Launch();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "DragIn1 was installed, but Windows would not start it.\r\n\r\n" +
                    ex.Message + "\r\n\r\n" +
                    "You can start it yourself from the Start Menu.",
                    "Could not open DragIn1",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void OnUninstall(object s, EventArgs e)
        {
            DialogResult r = MessageBox.Show(this,
                "Remove DragIn1?\r\n\r\n" +
                "Yes  -  remove it and delete the files it has captured.\r\n" +
                "No   -  remove it but keep those files.",
                "Uninstall DragIn1", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return;
            Actions.Uninstall(r == DialogResult.No);
            MessageBox.Show(this, "DragIn1 has been removed.", "Uninstall DragIn1",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] argv)
        {
            bool silent = false, uninstall = false;
            foreach (string a in argv)
            {
                string x = a.TrimStart('-', '/').ToLowerInvariant();
                if (x == "silent" || x == "s" || x == "quiet" || x == "q") silent = true;
                if (x == "uninstall" || x == "u" || x == "remove") uninstall = true;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (uninstall)
            {
                string dir = Info.ExistingInstallDir();
                if (dir != null) Info.InstallDir = dir;

                if (silent) { Actions.Uninstall(true); return; }
                DialogResult r = MessageBox.Show(
                    "Remove DragIn1?\r\n\r\n" +
                    "Yes  -  remove it and delete the files it has captured.\r\n" +
                    "No   -  remove it but keep those files.",
                    "Uninstall DragIn1", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                Actions.Uninstall(r == DialogResult.No);
                MessageBox.Show("DragIn1 has been removed.", "Uninstall DragIn1",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (silent)
            {
                try
                {
                    Actions.Install(true, false, true);
                    Actions.Launch();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "DragIn1 Setup failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            Application.Run(new SetupForm());
        }
    }
}
