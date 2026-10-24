using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    internal sealed class EditableText
    {
        internal readonly string Path, Original;
        private readonly byte[] hash;
        private readonly Encoding encoding;
        private readonly bool bom;
        internal EditableText(string path)
        {
            Path = path; if (!ArtifactText.IsText(path) || new FileInfo(path).Length > 2 * 1024 * 1024) throw new NotSupportedException("Editor aceita textos de até 2 MB.");
            var bytes = File.ReadAllBytes(path); int offset = 0; encoding = new UTF8Encoding(false, true);
            if (bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191) { bom = true; offset = 3; }
            else if (bytes.Length >= 2 && bytes[0] == 255 && bytes[1] == 254) { encoding = new UnicodeEncoding(false, true, true); bom = true; offset = 2; }
            else if (bytes.Length >= 2 && bytes[0] == 254 && bytes[1] == 255) { encoding = new UnicodeEncoding(true, true, true); bom = true; offset = 2; }
            Original = encoding.GetString(bytes, offset, bytes.Length - offset); if (Original.IndexOf('\0') >= 0) throw new InvalidDataException("Arquivo binário não pode ser editado como texto."); using (var sha = SHA256.Create()) hash = sha.ComputeHash(bytes);
        }
        internal void Save(string text, string sandbox)
        {
            if (sandbox == "read-only") throw new UnauthorizedAccessException("Esta conversa está em leitura. Altere /permissions antes de salvar.");
            using (var sha = SHA256.Create()) if (!sha.ComputeHash(File.ReadAllBytes(Path)).SequenceEqual(hash)) throw new IOException("O arquivo mudou no disco. Reabra para não sobrescrever alterações externas.");
            string normalized = text.Replace("\r\n", "\n"); if (Original.Contains("\r\n")) normalized = normalized.Replace("\n", "\r\n");
            byte[] body = encoding.GetBytes(normalized); byte[] prefix = bom ? (encoding is UTF8Encoding ? new byte[] { 239, 187, 191 } : encoding.GetPreamble()) : new byte[0];
            string temporary = Path + ".negotchi-" + Guid.NewGuid().ToString("N") + ".tmp";
            try { using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(prefix, 0, prefix.Length); output.Write(body, 0, body.Length); output.Flush(true); } File.Replace(temporary, Path, null); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    internal sealed class TextEditorWindow : Window
    {
        private EditableText document;
        private readonly TextBox editor;
        private readonly TextBlock status;
        private readonly string rootPath, sandbox;
        private bool dirty;
        public TextEditorWindow(Window owner, string rootPath, string path, string sandbox, Action<string> annotate)
        {
            this.rootPath = rootPath; this.sandbox = sandbox; document = new EditableText(WorkspaceFiles.Resolve(rootPath, path)); Owner = owner; Title = "Editar | " + System.IO.Path.GetFileName(path); Width = 900; Height = 650; MinWidth = 550; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text; ShowInTaskbar = false;
            var root = new DockPanel { Margin = new Thickness(20) }; status = new TextBlock { Text = "Edições locais não são salvas nem enviadas automaticamente.", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(status, Dock.Top); root.Children.Add(status);
            var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
            editor = new TextBox { Text = document.Original, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, FontFamily = Theme.FontWithFallback("Consolas"), Padding = new Thickness(10) }; root.Children.Add(editor);
            editor.TextChanged += delegate { dirty = editor.Text != document.Original; Title = (dirty ? "* " : "") + "Editar | " + System.IO.Path.GetFileName(document.Path); };
            actions.Children.Add(Theme.Button("Salvar", delegate { try { WorkspaceFiles.Resolve(this.rootPath, document.Path); if (this.sandbox == "read-only") throw new UnauthorizedAccessException("Conversa em leitura. Use /permissions antes de editar o arquivo no disco."); if (!DesktopActions.Review(this, "Salvar arquivo", document.Path + "\n\nGravar as alterações locais?")) return; document.Save(editor.Text, this.sandbox); document = new EditableText(document.Path); editor.Text = document.Original; dirty = false; Title = "Editar | " + System.IO.Path.GetFileName(document.Path); status.Text = "Arquivo salvo."; } catch (Exception error) { status.Text = error.Message; } }, true));
            actions.Children.Add(Theme.Button("Anotar seleção", delegate {
                if (editor.SelectionLength == 0) { status.Text = "Selecione um trecho para anotar."; return; }
                if (editor.SelectionLength > 8000) { status.Text = "Selecione até 8 mil caracteres."; return; }
                string note = CommandDialogs.Prompt(this, "Anotação", "A anotação e o trecho irão para o rascunho da conversa.", ""); if (note == null) return;
                int line = editor.Text.Substring(0, editor.SelectionStart).Count(character => character == '\n') + 1;
                annotate("Arquivo: \"" + document.Path + "\"\nLinha " + line + (dirty ? " (trecho da edição ainda não salva)" : "") + "\nTrecho selecionado:\n" + editor.SelectedText + "\n\nPedido do usuário: " + note + "\n"); status.Text = "Anotação adicionada ao rascunho; envie quando quiser.";
            }, false));
            actions.Children.Add(Theme.Button("Fechar", delegate { Close(); }, false)); Content = root; Theme.BindTypography(this);
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs args) { if (dirty && !DesktopActions.Review(this, "Descartar edição local?", "Há alterações não salvas em " + document.Path + ". Fechar sem gravar?")) args.Cancel = true; };
        }
    }
}
