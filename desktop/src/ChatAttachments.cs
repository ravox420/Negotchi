using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Negotchi
{
    internal sealed partial class ChatWindow
    {
        private readonly List<PendingAttachment> attachments = new List<PendingAttachment>();
        private StackPanel attachmentRows;
        private ScrollViewer attachmentScroll;
        public List<PendingAttachment> GetAttachments() { return attachments.ToList(); }
        public string Draft { get { return input.Text; } set { input.Text = value ?? ""; } }
        public void RestoreComposer(string draft, IEnumerable<PendingAttachment> saved)
        {
            Draft = draft; attachments.Clear(); if (saved != null) attachments.AddRange(saved.Where(item => item != null && !string.IsNullOrEmpty(item.Path)).Take(8)); RefreshAttachments();
        }
        public void ClearAttachments() { attachments.Clear(); RefreshAttachments(); }
        private void InitializeAttachments(StackPanel bottom)
        {
            attachmentRows = new StackPanel(); attachmentScroll = new ScrollViewer { Content = attachmentRows, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 6) }; bottom.Children.Add(attachmentScroll);
            AllowDrop = true;
            PreviewDragOver += delegate(object sender, DragEventArgs args) { if (args.Data.GetDataPresent(DataFormats.FileDrop)) { args.Effects = turnBusy || commandBusy ? DragDropEffects.None : DragDropEffects.Copy; args.Handled = true; } };
            PreviewDrop += delegate(object sender, DragEventArgs args) { if (turnBusy || commandBusy || !args.Data.GetDataPresent(DataFormats.FileDrop)) return; args.Handled = true; foreach (string path in (string[])args.Data.GetData(DataFormats.FileDrop)) AddAttachment(path); };
            DataObject.AddPastingHandler(input, delegate(object sender, DataObjectPastingEventArgs args) {
                if (turnBusy || commandBusy) return;
                if (args.DataObject.GetDataPresent(DataFormats.Bitmap)) { args.CancelCommand(); PasteImage(); }
                else if (args.DataObject.GetDataPresent(DataFormats.FileDrop)) { args.CancelCommand(); foreach (string path in (string[])args.DataObject.GetData(DataFormats.FileDrop)) AddAttachment(path); }
            });
        }
        private void AddAttachment(string path)
        {
            try
            {
                var attachment = AttachmentInput.Inspect(path);
                if (attachments.Any(item => string.Equals(item.Path, attachment.Path, StringComparison.OrdinalIgnoreCase))) return;
                if (attachments.Count >= 8 || attachments.Sum(item => item.Size) + attachment.Size > 500L * 1024 * 1024) throw new ArgumentException("Use até 8 anexos, no máximo 500 MB no total.");
                attachments.Add(attachment); RefreshAttachments();
            }
            catch (Exception error) { SetStatus("Não foi possível anexar: " + error.Message); }
        }
        private void ChooseAttachments()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Anexar arquivos — conteúdo enviado somente ao enviar", Multiselect = true, CheckFileExists = true }; if (dialog.ShowDialog(this) != true) return;
            foreach (string path in dialog.FileNames) AddAttachment(path);
        }
        private void PasteImage()
        {
            try
            {
                var bitmap = Clipboard.GetImage(); if (bitmap == null) return;
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Negotchi", "clipboard"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "imagem-" + Guid.NewGuid().ToString("N") + ".png");
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
                AddAttachment(path);
            }
            catch (Exception error) { SetStatus("Não foi possível colar imagem: " + error.Message); }
        }
        private void RefreshAttachments()
        {
            if (attachmentRows == null) return;
            attachmentRows.Children.Clear(); attachmentScroll.Visibility = attachments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            foreach (var attachment in attachments)
            {
                var row = new DockPanel(); var remove = Theme.Button("×", delegate { attachments.Remove(attachment); RefreshAttachments(); }, false); remove.Padding = new Thickness(5, 0, 5, 0); remove.MinHeight = 20; remove.IsEnabled = !turnBusy && !commandBusy; remove.ToolTip = "Remover anexo antes do envio"; DockPanel.SetDock(remove, Dock.Right); row.Children.Add(remove);
                row.Children.Add(new TextBlock { Text = attachment.Display, Foreground = Theme.Muted, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = attachment.Path });
                var item = new StackPanel { Margin = new Thickness(0, 0, 0, 5) }; item.Children.Add(row);
                if (attachment.Kind == "image" || attachment.Kind == "gif" || attachment.Kind == "video")
                {
                    try { item.Children.Add(MediaAttachmentView.Create(attachment, 210)); }
                    catch (Exception error) { item.Children.Add(new TextBlock { Text = "Prévia indisponível: " + error.Message, Foreground = Theme.Muted, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) }); }
                }
                attachmentRows.Children.Add(item);
            }
        }
    }
}
