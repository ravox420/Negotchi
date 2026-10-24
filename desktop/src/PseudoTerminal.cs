using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Negotchi
{
    internal sealed class PseudoTerminal : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct Coord { public short X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct SI { public int cb; public string r, d, t; public int x, y, xs, ys, xc, yc, fill, flags; public short show, reserved; public IntPtr reserved2, input, output, error; }
        [StructLayout(LayoutKind.Sequential)] private struct SIX { public SI StartupInfo; public IntPtr AttributeList; }
        [StructLayout(LayoutKind.Sequential)] private struct PI { public IntPtr Process, Thread; public int ProcessId, ThreadId; }
        [StructLayout(LayoutKind.Sequential)] private struct SA { public int Length; public IntPtr Descriptor; public bool Inherit; }
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool CreatePipe(out IntPtr r, out IntPtr w, ref SA a, int s);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetHandleInformation(IntPtr h, int m, int f);
        [DllImport("kernel32.dll", SetLastError=true)] static extern int CreatePseudoConsole(Coord c, IntPtr i, IntPtr o, int f, out IntPtr p);
        [DllImport("kernel32.dll")] static extern void ClosePseudoConsole(IntPtr p);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool InitializeProcThreadAttributeList(IntPtr l,int c,int f,ref IntPtr s);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool UpdateProcThreadAttribute(IntPtr l,int f,IntPtr a,IntPtr v,IntPtr s,IntPtr p,IntPtr r);
        [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr l);
        [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)] static extern bool CreateProcess(string a,string c,IntPtr pa,IntPtr ta,bool i,int f,IntPtr e,string d,ref SIX s,out PI p);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        IntPtr pty, process, attributes, inputHandle, outputHandle; StreamWriter input;
        public event Action<string> Output;
        public bool Running { get { return process != IntPtr.Zero; } }
        public void Start(string folder)
        {
            var sa = new SA { Length=Marshal.SizeOf(typeof(SA)), Inherit=true }; IntPtr inputRead, outputWrite;
            if(!CreatePipe(out inputRead,out inputHandle,ref sa,0)||!CreatePipe(out outputHandle,out outputWrite,ref sa,0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try { SetHandleInformation(inputHandle,1,0); SetHandleInformation(outputHandle,1,0); int result=CreatePseudoConsole(new Coord{X=120,Y=40},inputRead,outputWrite,0,out pty); if(result!=0) throw new System.ComponentModel.Win32Exception(result,"ConPTY indisponível."); } finally { CloseHandle(inputRead); CloseHandle(outputWrite); }
            IntPtr size=IntPtr.Zero; InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size); attributes=Marshal.AllocHGlobal(size); if(!InitializeProcThreadAttributeList(attributes,1,0,ref size)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); IntPtr value=Marshal.AllocHGlobal(IntPtr.Size); Marshal.WriteIntPtr(value,pty);
            try { if(!UpdateProcThreadAttribute(attributes,0,(IntPtr)0x00020016,value,(IntPtr)IntPtr.Size,IntPtr.Zero,IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); var si=new SIX(); si.StartupInfo.cb=Marshal.SizeOf(typeof(SIX)); si.AttributeList=attributes; PI pi; if(!CreateProcess(null,"powershell.exe -NoLogo -NoProfile -NoExit",IntPtr.Zero,IntPtr.Zero,false,0x00080000,IntPtr.Zero,folder,ref si,out pi)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); process=pi.Process; CloseHandle(pi.Thread); input=new StreamWriter(new FileStream(new SafeFileHandle(inputHandle,true),FileAccess.Write,4096,false),new UTF8Encoding(false)){AutoFlush=true}; inputHandle=IntPtr.Zero; Read(); } finally { Marshal.FreeHGlobal(value); }
        }
        async void Read() { try { using(var stream=new FileStream(new SafeFileHandle(outputHandle,true),FileAccess.Read,4096,true)){ outputHandle=IntPtr.Zero; var b=new byte[4096]; int n; while((n=await stream.ReadAsync(b,0,b.Length))>0) if(Output!=null) Output(Encoding.UTF8.GetString(b,0,n)); } } catch(ObjectDisposedException){} finally{process=IntPtr.Zero;} }
        public void Send(string text){if(input!=null)input.Write(text+"\r\n");}
        public void Dispose(){if(input!=null)input.Dispose();if(pty!=IntPtr.Zero){ClosePseudoConsole(pty);pty=IntPtr.Zero;}if(process!=IntPtr.Zero){CloseHandle(process);process=IntPtr.Zero;}if(attributes!=IntPtr.Zero){DeleteProcThreadAttributeList(attributes);Marshal.FreeHGlobal(attributes);attributes=IntPtr.Zero;}if(inputHandle!=IntPtr.Zero)CloseHandle(inputHandle);if(outputHandle!=IntPtr.Zero)CloseHandle(outputHandle);}
    }
}
