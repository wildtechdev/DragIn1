// DragIn1 desktop - fixes drag and drop out of New Outlook, Teams and web apps.
//
// Why this works: Chromium-based apps (New Outlook, Outlook Web, Teams, Gmail,
// SharePoint) advertise dragged files through IDataObjectAsyncCapability with
// asyncMode=TRUE. They will NOT render the bytes to a drop target that just calls
// GetData(CF_HDROP) synchronously - you get DV_E_FORMATETC. The file only
// materializes if the target performs the documented handshake:
//     GetAsyncMode -> StartOperation -> (extract off the UI thread) -> EndOperation
// Almost no app does this, which is why "drag and drop is broken" in New Outlook.
// DragIn1 does it, copies the result somewhere permanent, and re-serves it as a
// plain CF_HDROP native drag that any Windows app or web dropzone accepts.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace DragIn1App
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINTL { public int x; public int y; }

    [ComImport, Guid("00000122-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleDropTarget
    {
        [PreserveSig] int DragEnter(ComIDataObject pDataObj, int grfKeyState, POINTL pt, ref int pdwEffect);
        [PreserveSig] int DragOver(int grfKeyState, POINTL pt, ref int pdwEffect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop(ComIDataObject pDataObj, int grfKeyState, POINTL pt, ref int pdwEffect);
    }

    [ComImport, Guid("3D8B0590-F691-11d2-8EA9-006097DF5BD4"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDataObjectAsyncCapability
    {
        [PreserveSig] int SetAsyncMode(int fDoOpAsync);
        [PreserveSig] int GetAsyncMode(out int pfIsOpAsync);
        [PreserveSig] int StartOperation(IntPtr pbcReserved);
        [PreserveSig] int InOperation(out int pfInAsyncOp);
        [PreserveSig] int EndOperation(int hResult, IntPtr pbcReserved, int dwEffects);
    }

    public static class Native
    {
        [DllImport("ole32.dll")] public static extern int OleInitialize(IntPtr p);
        [DllImport("ole32.dll")] public static extern int RegisterDragDrop(IntPtr hwnd, IOleDropTarget t);
        [DllImport("ole32.dll")] public static extern int RevokeDragDrop(IntPtr hwnd);
        [DllImport("ole32.dll")] public static extern void ReleaseStgMedium(ref STGMEDIUM m);
        [DllImport("ole32.dll")] public static extern int CoMarshalInterThreadInterfaceInStream(ref Guid riid, IntPtr pUnk, out IntPtr ppStm);
        [DllImport("ole32.dll")] public static extern int CoGetInterfaceAndReleaseStream(IntPtr pStm, ref Guid riid, out IntPtr ppv);
        [DllImport("kernel32.dll")] public static extern IntPtr GlobalLock(IntPtr h);
        [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(IntPtr h);
        [DllImport("kernel32.dll")] public static extern int GlobalSize(IntPtr h);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder f, uint cch);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterClipboardFormat(string s);
    }

    public static class Store
    {
        public static string Root;
        public static string Session;

        public static void Init()
        {
            Root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DragIn1");
            Session = Path.Combine(Root, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(Session);
            PruneOld();
        }

        static void PruneOld()
        {
            try
            {
                foreach (string d in Directory.GetDirectories(Root))
                {
                    try
                    {
                        if (d == Session) continue;
                        if (Directory.GetLastWriteTime(d) < DateTime.Now.AddDays(-7))
                            Directory.Delete(d, true);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static string Reserve(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            if (fileName.Length == 0) fileName = "file";
            string target = Path.Combine(Session, fileName);
            if (!File.Exists(target)) return target;
            string base_ = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            for (int i = 2; i < 1000; i++)
            {
                target = Path.Combine(Session, base_ + " (" + i + ")" + ext);
                if (!File.Exists(target)) return target;
            }
            return Path.Combine(Session, Guid.NewGuid().ToString("N") + ext);
        }
    }

    // Pulls real bytes out of a drag source, including Chromium async sources.
    public static class Grab
    {
        public static uint CF_HDROP = 15;
        public static uint CF_FDW = Native.RegisterClipboardFormat("FileGroupDescriptorW");
        public static uint CF_FC = Native.RegisterClipboardFormat("FileContents");

        public static List<string> Hdrop(ComIDataObject data)
        {
            List<string> outp = new List<string>();
            FORMATETC fe = new FORMATETC();
            fe.cfFormat = (short)CF_HDROP;
            fe.dwAspect = DVASPECT.DVASPECT_CONTENT;
            fe.lindex = -1;
            fe.ptd = IntPtr.Zero;
            fe.tymed = TYMED.TYMED_HGLOBAL;
            STGMEDIUM stg = new STGMEDIUM();
            try { data.GetData(ref fe, out stg); }
            catch { return outp; }
            if (stg.unionmember == IntPtr.Zero) return outp;
            try
            {
                IntPtr h = Native.GlobalLock(stg.unionmember);
                uint n = Native.DragQueryFile(h, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < n; i++)
                {
                    StringBuilder sb = new StringBuilder(2048);
                    Native.DragQueryFile(h, i, sb, 2048);
                    string p = sb.ToString();
                    if (p.Length > 0) outp.Add(p);
                }
                Native.GlobalUnlock(stg.unionmember);
            }
            catch { }
            finally { try { Native.ReleaseStgMedium(ref stg); } catch { } }
            return outp;
        }

        public static List<string> Virtual(ComIDataObject data)
        {
            List<string> made = new List<string>();
            List<string> names = new List<string>();
            FORMATETC fd = new FORMATETC();
            fd.cfFormat = (short)CF_FDW;
            fd.dwAspect = DVASPECT.DVASPECT_CONTENT;
            fd.lindex = -1;
            fd.ptd = IntPtr.Zero;
            fd.tymed = TYMED.TYMED_HGLOBAL;
            STGMEDIUM sd = new STGMEDIUM();
            try { data.GetData(ref fd, out sd); }
            catch { return made; }
            if (sd.unionmember == IntPtr.Zero) return made;
            try
            {
                IntPtr p = Native.GlobalLock(sd.unionmember);
                int count = Marshal.ReadInt32(p);
                for (int i = 0; i < count; i++)
                {
                    IntPtr b = new IntPtr(p.ToInt64() + 4 + (i * 592) + 72);
                    names.Add(Marshal.PtrToStringUni(b));
                }
                Native.GlobalUnlock(sd.unionmember);
            }
            catch { }
            finally { try { Native.ReleaseStgMedium(ref sd); } catch { } }

            for (int i = 0; i < names.Count; i++)
            {
                FORMATETC fc = new FORMATETC();
                fc.cfFormat = (short)CF_FC;
                fc.dwAspect = DVASPECT.DVASPECT_CONTENT;
                fc.lindex = i;
                fc.ptd = IntPtr.Zero;
                fc.tymed = TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL;
                STGMEDIUM sc = new STGMEDIUM();
                try { data.GetData(ref fc, out sc); }
                catch { continue; }
                try
                {
                    byte[] bytes = null;
                    if (sc.tymed == TYMED.TYMED_ISTREAM && sc.unionmember != IntPtr.Zero)
                        bytes = ReadStream((IStream)Marshal.GetObjectForIUnknown(sc.unionmember));
                    else if (sc.tymed == TYMED.TYMED_HGLOBAL && sc.unionmember != IntPtr.Zero)
                    {
                        int len = Native.GlobalSize(sc.unionmember);
                        IntPtr lp = Native.GlobalLock(sc.unionmember);
                        bytes = new byte[len];
                        Marshal.Copy(lp, bytes, 0, len);
                        Native.GlobalUnlock(sc.unionmember);
                    }
                    if (bytes != null && bytes.Length > 0)
                    {
                        string dst = Store.Reserve(names[i]);
                        File.WriteAllBytes(dst, bytes);
                        made.Add(dst);
                    }
                }
                catch { }
                finally { try { Native.ReleaseStgMedium(ref sc); } catch { } }
            }
            return made;
        }

        static byte[] ReadStream(IStream stm)
        {
            System.Runtime.InteropServices.ComTypes.STATSTG st;
            stm.Stat(out st, 1);
            long len = st.cbSize;
            MemoryStream ms = new MemoryStream();
            byte[] buf = new byte[81920];
            IntPtr read = Marshal.AllocCoTaskMem(8);
            try
            {
                try { stm.Seek(0, 0, IntPtr.Zero); }
                catch { }
                while (true)
                {
                    stm.Read(buf, buf.Length, read);
                    int n = Marshal.ReadInt32(read);
                    if (n <= 0) break;
                    ms.Write(buf, 0, n);
                    if (len > 0 && ms.Length >= len) break;
                }
            }
            finally { Marshal.FreeCoTaskMem(read); }
            return ms.ToArray();
        }

        // Copy into our own store so Chromium deleting its temp dir cannot bite us.
        public static List<string> Persist(List<string> paths)
        {
            List<string> kept = new List<string>();
            foreach (string p in paths)
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    string dst = Store.Reserve(Path.GetFileName(p));
                    File.Copy(p, dst, false);
                    kept.Add(dst);
                }
                catch { }
            }
            return kept;
        }
    }

    [ClassInterface(ClassInterfaceType.None)]
    public class ShelfTarget : IOleDropTarget
    {
        static Guid IID_IDataObject = new Guid("0000010e-0000-0000-C000-000000000046");
        public MainForm Owner;
        public bool SelfDrag;

        public int DragEnter(ComIDataObject d, int k, POINTL p, ref int eff)
        {
            eff = SelfDrag ? 0 : 1;
            if (!SelfDrag) Owner.SetHot(true);
            return 0;
        }
        public int DragOver(int k, POINTL p, ref int eff) { eff = SelfDrag ? 0 : 1; return 0; }
        public int DragLeave() { Owner.SetHot(false); return 0; }

        public int Drop(ComIDataObject data, int k, POINTL p, ref int eff)
        {
            eff = 1;
            Owner.SetHot(false);
            if (SelfDrag) return 0;

            // Fast path: a real file drag (Explorer, or an already-materialized source).
            List<string> direct = Grab.Hdrop(data);
            if (direct.Count > 0)
            {
                Owner.AddFiles(Grab.Persist(direct), "dropped");
                return 0;
            }

            // Chromium async path: the reason this app exists.
            IDataObjectAsyncCapability async = data as IDataObjectAsyncCapability;
            int isAsync = 0;
            if (async != null) async.GetAsyncMode(out isAsync);

            if (async == null || isAsync == 0)
            {
                List<string> virt = Grab.Virtual(data);
                if (virt.Count > 0) Owner.AddFiles(virt, "extracted");
                else Owner.Status("Nothing usable in that drag.");
                return 0;
            }

            Owner.Status("Fetching from Outlook...");
            async.StartOperation(IntPtr.Zero);

            IntPtr pUnk = Marshal.GetIUnknownForObject(data);
            IntPtr pStm;
            int hr = Native.CoMarshalInterThreadInterfaceInStream(ref IID_IDataObject, pUnk, out pStm);
            Marshal.Release(pUnk);
            if (hr != 0)
            {
                Owner.Status("Could not marshal the drag (0x" + hr.ToString("X8") + ").");
                return 0;
            }

            Thread t = new Thread(delegate()
            {
                List<string> got = new List<string>();
                ComIDataObject d2 = null;
                try
                {
                    IntPtr pv;
                    Guid iid = IID_IDataObject;
                    if (Native.CoGetInterfaceAndReleaseStream(pStm, ref iid, out pv) != 0) return;
                    d2 = (ComIDataObject)Marshal.GetObjectForIUnknown(pv);
                    Marshal.Release(pv);

                    // Chromium writes the file asynchronously; poll briefly.
                    for (int i = 0; i < 60 && got.Count == 0; i++)
                    {
                        List<string> h = Grab.Hdrop(d2);
                        if (h.Count > 0) { got = Grab.Persist(h); break; }
                        List<string> v = Grab.Virtual(d2);
                        if (v.Count > 0) { got = v; break; }
                        Thread.Sleep(250);
                    }
                }
                catch { }
                finally
                {
                    try
                    {
                        IDataObjectAsyncCapability a2 = d2 as IDataObjectAsyncCapability;
                        if (a2 != null) a2.EndOperation(0, IntPtr.Zero, 1);
                    }
                    catch { }
                }
                if (got.Count > 0) Owner.AddFiles(got, "captured");
                else Owner.Status("Source never delivered the file. Try again.");
            });
            t.SetApartmentState(ApartmentState.MTA);
            t.IsBackground = true;
            t.Start();
            return 0;
        }
    }

    public class MainForm : Form
    {
        ListView list;
        Label hint;
        StatusStrip strip;
        ToolStripStatusLabel status;
        ImageList icons;
        ShelfTarget target;
        Color idle = Color.FromArgb(250, 250, 252);
        Color hot = Color.FromArgb(214, 240, 214);

        public MainForm()
        {
            Text = "DragIn1";
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            Size = new Size(430, 330);
            MinimumSize = new Size(320, 200);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(
                Screen.PrimaryScreen.WorkingArea.Right - 450,
                Screen.PrimaryScreen.WorkingArea.Bottom - 380);
            TopMost = true;
            ShowInTaskbar = true;
            BackColor = idle;

            icons = new ImageList();
            icons.ImageSize = new Size(32, 32);
            icons.ColorDepth = ColorDepth.Depth32Bit;

            list = new ListView();
            list.Dock = DockStyle.Fill;
            list.View = View.Tile;
            list.TileSize = new Size(330, 40);
            list.LargeImageList = icons;
            list.FullRowSelect = true;
            list.MultiSelect = true;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = idle;
            list.ItemDrag += OnItemDrag;
            list.DoubleClick += delegate { OpenSelected(); };
            list.KeyDown += OnKey;

            hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleCenter;
            hint.Font = new Font("Segoe UI", 10);
            hint.ForeColor = Color.FromArgb(110, 110, 120);
            hint.Text = "Drop attachments here.\r\nThen drag them anywhere you like.";

            strip = new StatusStrip();
            status = new ToolStripStatusLabel("Ready");
            status.Spring = true;
            status.TextAlign = ContentAlignment.MiddleLeft;
            strip.Items.Add(status);

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, delegate { OpenSelected(); });
            menu.Items.Add("Copy to clipboard", null, delegate { CopySelected(); });
            menu.Items.Add("Show in folder", null, delegate { ShowInFolder(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Remove selected", null, delegate { RemoveSelected(); });
            menu.Items.Add("Clear all", null, delegate { list.Items.Clear(); Sync(); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem pin = new ToolStripMenuItem("Always on top");
            pin.Checked = true;
            pin.Click += delegate { pin.Checked = !pin.Checked; TopMost = pin.Checked; };
            menu.Items.Add(pin);
            ToolStripMenuItem startup = new ToolStripMenuItem("Start with Windows");
            startup.Checked = Startup.IsEnabled();
            startup.Click += delegate
            {
                startup.Checked = !startup.Checked;
                Startup.Set(startup.Checked);
            };
            menu.Items.Add(startup);
            list.ContextMenuStrip = menu;

            Controls.Add(list);
            Controls.Add(hint);
            Controls.Add(strip);
            hint.BringToFront();

            target = new ShelfTarget();
            target.Owner = this;

            Shown += delegate
            {
                try { Application.OleRequired(); }
                catch { }
                Native.OleInitialize(IntPtr.Zero);
                foreach (Control c in new Control[] { this, list, hint })
                {
                    Native.RevokeDragDrop(c.Handle);
                    Native.RegisterDragDrop(c.Handle, target);
                }
                Status("Ready. Drop an Outlook attachment here.");
            };
            FormClosing += delegate
            {
                foreach (Control c in new Control[] { this, list, hint })
                {
                    try { Native.RevokeDragDrop(c.Handle); }
                    catch { }
                }
            };
        }

        void OnKey(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) RemoveSelected();
            if (e.Control && e.KeyCode == Keys.C) CopySelected();
            if (e.Control && e.KeyCode == Keys.A)
                foreach (ListViewItem i in list.Items) i.Selected = true;
        }

        public void SetHot(bool on)
        {
            try
            {
                BeginInvoke(new Action(delegate
                {
                    BackColor = on ? hot : idle;
                    list.BackColor = on ? hot : idle;
                    hint.BackColor = on ? hot : idle;
                }));
            }
            catch { }
        }

        public void Status(string s)
        {
            try { BeginInvoke(new Action(delegate { status.Text = s; })); }
            catch { }
        }

        public void AddFiles(List<string> paths, string verb)
        {
            try
            {
                BeginInvoke(new Action(delegate
                {
                    foreach (string p in paths)
                    {
                        string key = p;
                        try
                        {
                            if (!icons.Images.ContainsKey(key))
                            {
                                Icon ic = Icon.ExtractAssociatedIcon(p);
                                if (ic != null) icons.Images.Add(key, ic.ToBitmap());
                            }
                        }
                        catch { }
                        ListViewItem it = new ListViewItem(Path.GetFileName(p));
                        it.Tag = p;
                        it.ToolTipText = p;
                        if (icons.Images.ContainsKey(key)) it.ImageKey = key;
                        long kb = 0;
                        try { kb = new FileInfo(p).Length / 1024; }
                        catch { }
                        it.SubItems.Add(kb + " KB");
                        list.Items.Add(it);
                    }
                    Sync();
                    status.Text = paths.Count + " file(s) " + verb + ". Drag them out from here.";
                }));
            }
            catch { }
        }

        void Sync()
        {
            hint.Visible = list.Items.Count == 0;
            if (hint.Visible) hint.BringToFront();
            list.ShowItemToolTips = true;
        }

        List<string> Selected()
        {
            List<string> r = new List<string>();
            foreach (ListViewItem i in list.SelectedItems)
                if (i.Tag != null && File.Exists((string)i.Tag)) r.Add((string)i.Tag);
            return r;
        }

        void OnItemDrag(object s, ItemDragEventArgs e)
        {
            List<string> paths = Selected();
            if (paths.Count == 0 && e.Item != null)
            {
                string p = (string)((ListViewItem)e.Item).Tag;
                if (File.Exists(p)) paths.Add(p);
            }
            if (paths.Count == 0) return;

            DataObject d = new DataObject();
            d.SetData(DataFormats.FileDrop, true, paths.ToArray());
            d.SetData(DataFormats.Text, string.Join("\r\n", paths.ToArray()));

            target.SelfDrag = true;
            try { list.DoDragDrop(d, DragDropEffects.Copy | DragDropEffects.Link); }
            catch { }
            finally { target.SelfDrag = false; }
        }

        void OpenSelected()
        {
            foreach (string p in Selected())
                try { System.Diagnostics.Process.Start(p); }
                catch { }
        }

        void CopySelected()
        {
            List<string> p = Selected();
            if (p.Count == 0) return;
            System.Collections.Specialized.StringCollection sc =
                new System.Collections.Specialized.StringCollection();
            foreach (string x in p) sc.Add(x);
            Clipboard.SetFileDropList(sc);
            Status(p.Count + " file(s) on the clipboard. Ctrl+V into any upload dialog.");
        }

        void ShowInFolder()
        {
            List<string> p = Selected();
            if (p.Count == 0) { System.Diagnostics.Process.Start(Store.Session); return; }
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + p[0] + "\"");
        }

        void RemoveSelected()
        {
            List<ListViewItem> gone = new List<ListViewItem>();
            foreach (ListViewItem i in list.SelectedItems) gone.Add(i);
            foreach (ListViewItem i in gone) list.Items.Remove(i);
            Sync();
        }
    }

    public static class Startup
    {
        const string Key = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        const string Name = "DragIn1";

        public static bool IsEnabled()
        {
            try
            {
                Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key);
                if (k == null) return false;
                object v = k.GetValue(Name);
                k.Close();
                return v != null;
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            try
            {
                Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key, true);
                if (k == null) return;
                if (on) k.SetValue(Name, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(Name, false);
                k.Close();
            }
            catch { }
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Store.Init();
            Application.Run(new MainForm());
        }
    }
}
