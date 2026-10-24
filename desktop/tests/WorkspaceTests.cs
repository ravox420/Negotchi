using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Negotchi
{
    internal static partial class Tests
    {
        private static async Task TestWorkspace()
        {
            string directory = Path.Combine(output, "workspace-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string textFile = Path.Combine(directory, "acentuação.txt"); File.WriteAllText(textFile, "Anexo com ação e café 😀", new UTF8Encoding(false));
            var attachment = AttachmentInput.Inspect(textFile);
            Check(attachment.Kind == "text" && attachment.Name == "acentuação.txt", "anexo selecionado expoe somente nome tipo e tamanho");
            File.WriteAllText(textFile, "Conteúdo atualizado 😀", new UTF8Encoding(false));
            var input = AttachmentInput.Build("Veja o anexo", null, new[] { attachment });
            Check(input.Count == 2 && Json.Text((System.Collections.Generic.Dictionary<string, object>)input[1], "text").Contains("Conteúdo atualizado 😀"), "le o conteudo do anexo somente na preparacao do envio");
            File.Delete(textFile); bool missing = false; try { AttachmentInput.Build("", null, new[] { attachment }); } catch (FileNotFoundException) { missing = true; }
            Check(missing, "anexo removido do disco nao e enviado como se tivesse sido lido");
            File.WriteAllText(textFile, "Conteúdo da prévia", new UTF8Encoding(false));
            Check(ArtifactText.Read(textFile) == "Conteúdo da prévia", "previa textual preserva UTF-8");
            var editable = new EditableText(textFile); bool denied = false; try { editable.Save("não gravar", "read-only"); } catch (UnauthorizedAccessException) { denied = true; }
            Check(denied && File.ReadAllText(textFile) == "Conteúdo da prévia", "edicao em leitura nao modifica o arquivo");
            editable.Save("Edição aprovada 😀", "workspace-write"); Check(ArtifactText.Read(textFile) == "Edição aprovada 😀", "edicao preserva UTF-8 na gravacao atomica");
            editable = new EditableText(textFile); File.WriteAllText(textFile, "Alteração externa"); bool conflict = false; try { editable.Save("sobrescrever", "workspace-write"); } catch (IOException) { conflict = true; }
            Check(conflict && File.ReadAllText(textFile) == "Alteração externa", "editor impede sobrescrever alteracao externa");
            var hunks = GitReview.Hunks("@@ -2,3 +4,5 @@ função\n-antigo\n+novo\n@@ -20 +22 @@ outra\n"); Check(hunks.Count == 2 && hunks[1].After == 22, "revisao navega pelos trechos reais do patch Git");
            string subdirectory = Path.Combine(directory, "pasta"); Directory.CreateDirectory(subdirectory); File.WriteAllText(Path.Combine(subdirectory, "busca.txt"), "texto");
            Check(WorkspaceFiles.Search(directory, "busca", CancellationToken.None).Count == 1, "busca por nome percorre as subpastas do contexto");
            bool outside = false; try { WorkspaceFiles.Resolve(subdirectory, textFile); } catch (UnauthorizedAccessException) { outside = true; }
            Check(outside, "explorador recusa caminhos fora do contexto");
            string docx = Path.Combine(directory, "documento.docx");
            using (var zip = ZipFile.Open(docx, ZipArchiveMode.Create)) using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), Encoding.UTF8)) writer.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>Ação no documento</w:t></w:r></w:p></w:body></w:document>");
            Check(ArtifactText.Read(docx).Contains("Ação no documento"), "previa de DOCX extrai texto sem executar conteudo");
            string pdfFile = Path.Combine(directory, "preview.pdf");
            var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new System.Collections.Generic.List<int> { 0 };
            string[] objects = { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>", "<< /Length 42 >>\nstream\nBT /F1 16 Tf 30 100 Td (Negotchi) Tj ET\nendstream", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
            for (int i = 0; i < objects.Length; i++) { offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString())); pdf.Append((i + 1) + " 0 obj\n" + objects[i] + "\nendobj\n"); }
            int xref = Encoding.ASCII.GetByteCount(pdf.ToString()); pdf.Append("xref\n0 6\n0000000000 65535 f \n"); foreach (int offset in offsets.Skip(1)) pdf.Append(offset.ToString("D10") + " 00000 n \n"); pdf.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n"); File.WriteAllText(pdfFile, pdf.ToString(), Encoding.ASCII);
            var renderer = new PdfPreview(); await renderer.Load(pdfFile); var page = await renderer.Page(0);
            Check(renderer.PageCount == 1 && page.PixelWidth > 0 && page.PixelHeight > 0, "renderiza PDF real pela API nativa do Windows sem SDK externo");
        }
    }
}
