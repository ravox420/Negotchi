using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Negotchi
{
    // One stdio connection, bidirectional JSON-RPC, no terminal window.
    internal sealed class CodexClient : IDisposable
    {
        private Process process;
        private readonly object writeLock = new object(), pendingLock = new object();
        private readonly Dictionary<string, TaskCompletionSource<Dictionary<string, object>>> pending = new Dictionary<string, TaskCompletionSource<Dictionary<string, object>>>();
        private readonly SemaphoreSlim startup = new SemaphoreSlim(1, 1);
        private int nextId;
        private volatile bool ready, disposed;
        public event Action<string, Dictionary<string, object>> Notification;
        public event Action<string> Disconnected;
        public Func<string, Dictionary<string, object>, Task<object>> HandleRequest;
        public bool IsConnected { get { return ready && process != null && !process.HasExited; } }

        public static string FindExecutable()
        {
            string configured = Environment.GetEnvironmentVariable("NEGOTCHI_CODEX_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (!File.Exists(configured) || !configured.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new FileNotFoundException("NEGOTCHI_CODEX_PATH deve apontar para o codex.exe.");
                return Path.GetFullPath(configured);
            }
            var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(p => p.Trim().Trim('"')).Where(p => p.Length > 0).ToList();
            directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"));
            foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var direct = Path.Combine(directory, "codex.exe");
                if (File.Exists(direct)) return direct;
                foreach (var architecture in new[] { "x64", "arm64" })
                {
                    string target = architecture == "x64" ? "x86_64-pc-windows-msvc" : "aarch64-pc-windows-msvc";
                    var package = Path.Combine(directory, "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-" + architecture, "vendor", target);
                    foreach (var subdirectory in new[] { "bin", "codex" })
                    {
                        var candidate = Path.Combine(package, subdirectory, "codex.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            throw new FileNotFoundException("Codex nao encontrado. Instale o Codex CLI ou defina NEGOTCHI_CODEX_PATH com o caminho do codex.exe.");
        }

        public async Task StartAsync(string folder)
        {
            await startup.WaitAsync();
            try
            {
                if (disposed) throw new ObjectDisposedException("CodexClient");
                if (IsConnected) return;
                if (process != null)
                {
                    // A reconnect uses a fresh transport instead of reinitializing a dead one.
                    try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
                    process.Dispose(); process = null;
                }
                var info = new ProcessStartInfo(FindExecutable(), "app-server --listen stdio://") {
                    WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                var child = new Process { StartInfo = info };
                child.Start(); child.StandardInput.AutoFlush = true;
                process = child;
                // Always drain stderr so a full error pipe cannot block the agent.
                Background(async delegate { try { while (await child.StandardError.ReadLineAsync() != null) { } } catch (IOException) { } catch (ObjectDisposedException) { } });
                Background(() => ReadLoopAsync(child));
                await RequestAsync("initialize", Json.Map("clientInfo", Json.Map("name", "negotchi_desktop", "title", "Negotchi", "version", "0.1.0"), "capabilities", Json.Map("experimentalApi", true)));
                Write(Json.Map("method", "initialized", "params", Json.Map()));
                ready = true;
            }
            catch
            {
                ready = false;
                if (process != null) { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }
                throw;
            }
            finally { startup.Release(); }
        }

        private static void Background(Func<Task> work) { Task.Run(work); }

        public async Task<Dictionary<string, object>> RequestAsync(string method, object parameters, int timeoutMilliseconds = 45000)
        {
            if (disposed) throw new ObjectDisposedException("CodexClient");
            string key = Interlocked.Increment(ref nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var completion = new TaskCompletionSource<Dictionary<string, object>>();
            lock (pendingLock) pending.Add(key, completion);
            try
            {
                Write(Json.Map("id", int.Parse(key), "method", method, "params", parameters));
                if (await Task.WhenAny(completion.Task, Task.Delay(timeoutMilliseconds)) != completion.Task) throw new TimeoutException("O Codex demorou para responder. Tente reconectar.");
                return await completion.Task;
            }
            finally { lock (pendingLock) pending.Remove(key); }
        }

        private void Write(object message)
        {
            lock (writeLock)
            {
                if (process == null || process.HasExited) throw new IOException("O processo do Codex foi encerrado.");
                // Write UTF-8 bytes explicitly; .NET Framework's default stdin encoding can be ANSI.
                byte[] bytes = new UTF8Encoding(false).GetBytes(Json.Encode(message) + "\n");
                process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                process.StandardInput.BaseStream.Flush();
            }
        }

        private async Task ReadLoopAsync(Process child)
        {
            string failure = "A conexao com o Codex foi encerrada.";
            try
            {
                string line;
                while ((line = await child.StandardOutput.ReadLineAsync()) != null)
                {
                    Dictionary<string, object> message;
                    try { message = Json.Decode(line); } catch (ArgumentException) { continue; }
                    string method = Json.Text(message, "method");
                    object id = Json.Get(message, "id");
                    if (method.Length > 0)
                    {
                        var parameters = Json.Object(message, "params");
                        if (id != null)
                        {
                            // Server requests may await the UI; keep pumping notifications and responses.
                            HandleServerRequest(child, id, method, parameters);
                        }
                        else
                        {
                            var handler = Notification;
                            if (handler != null) handler(method, parameters);
                        }
                    }
                    else if (id != null)
                    {
                        TaskCompletionSource<Dictionary<string, object>> completion;
                        lock (pendingLock) pending.TryGetValue(Convert.ToString(id), out completion);
                        if (completion == null) continue;
                        if (Json.Get(message, "error") != null) completion.TrySetException(new InvalidOperationException(Json.Text(Json.Object(message, "error"), "message")));
                        else completion.TrySetResult(Json.Object(message, "result"));
                    }
                }
            }
            catch (Exception error) { failure = "Conexao com o Codex: " + error.Message; }
            finally
            {
                if (ReferenceEquals(child, process))
                {
                    ready = false;
                    lock (pendingLock) foreach (var completion in pending.Values) completion.TrySetException(new IOException(failure));
                    var handler = Disconnected;
                    if (!disposed && handler != null) handler(failure);
                }
            }
        }

        private async void HandleServerRequest(Process child, object id, string method, Dictionary<string, object> parameters)
        {
            try
            {
                if (HandleRequest == null) throw new NotSupportedException("Solicitacao nao suportada: " + method);
                object result = await HandleRequest(method, parameters);
                if (!disposed && ReferenceEquals(child, process)) Write(Json.Map("id", id, "result", result));
            }
            catch (Exception error)
            {
                try { if (!disposed && ReferenceEquals(child, process)) Write(Json.Map("id", id, "error", Json.Map("code", -32601, "message", error.Message))); }
                catch (Exception) { }
            }
        }

        public void Dispose()
        {
            disposed = true; ready = false;
            lock (pendingLock) foreach (var completion in pending.Values) completion.TrySetException(new ObjectDisposedException("CodexClient"));
            if (process == null) return;
            try
            {
                process.StandardInput.Close();
                if (!process.WaitForExit(1200)) process.Kill();
            }
            catch (InvalidOperationException) { }
            catch (IOException) { }
            process.Dispose();
        }
    }
}
