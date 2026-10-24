using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace Negotchi
{
    public sealed class ProcessReminder { public int ProcessId { get; set; } public string ProcessName { get; set; } public string Message { get; set; } }
    internal sealed class ProcessMonitor : IDisposable
    {
        private readonly DispatcherTimer timer; private readonly Func<ProcessReminder[]> reminders; private readonly Action<ProcessReminder> completed;
        internal ProcessMonitor(Func<ProcessReminder[]> reminders, Action<ProcessReminder> completed)
        {
            this.reminders = reminders; this.completed = completed; timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; timer.Tick += delegate { foreach (var reminder in reminders()) { try { using (var process = Process.GetProcessById(reminder.ProcessId)) { if (!process.HasExited) continue; } } catch (ArgumentException) { completed(reminder); } }; }; timer.Start();
        }
        public void Dispose() { timer.Stop(); }
    }
}
