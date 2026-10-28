using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negotchi
{
    internal sealed class StateStore
    {
        private const int ReplaceExisting = 0x1, WriteThrough = 0x8;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string source, string destination, int flags);
        private readonly string path;
        public StateStore(string directory) { path = Path.Combine(directory, "state.json"); }
        public AppState Load()
        {
            if (!File.Exists(path)) return new AppState();
            try
            {
                var state = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<AppState>(File.ReadAllText(path, Encoding.UTF8)) ?? new AppState();
                state.Messages = state.Messages ?? new List<ChatMessage>();
                state.Messages = state.Messages.Where(m => m != null && m.Text != null).ToList();
                foreach (var message in state.Messages) if (message.Role == "assistant" && string.IsNullOrEmpty(message.PetName)) message.PetName = "PimpDogg";
                if (double.IsNaN(state.Scale) || double.IsInfinity(state.Scale)) state.Scale = 1;
                state.Scale = Math.Max(0.65, Math.Min(1.8, state.Scale));
                if (double.IsNaN(state.X) || double.IsInfinity(state.X) || double.IsNaN(state.Y) || double.IsInfinity(state.Y)) { state.X = -1; state.Y = -1; }
                if (!Directory.Exists(state.Folder)) state.Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (state.Sandbox != "workspace-write" && state.Sandbox != "danger-full-access") state.Sandbox = "read-only";
                state.EnsureConversations(); state.LoadActiveConversation();
                state.Appearance = Theme.Normalize(state.Appearance);
                return state;
            }
            catch (IOException) { return new AppState(); }
            catch (ArgumentException) { return new AppState(); }
            catch (InvalidOperationException) { return new AppState(); }
        }
        public void Save(AppState state)
        {
            state.SaveActiveConversation();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, Json.Encode(state), new UTF8Encoding(false));
            // Rename the completed file on the same volume, without merging metadata from the old state.
            if (!MoveFileEx(temporary, path, ReplaceExisting | WriteThrough)) throw new IOException("Nao foi possivel salvar as preferencias.", new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }
}
