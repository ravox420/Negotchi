using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Foundation;

namespace Negotchi
{
    internal sealed class PdfPreview
    {
        private PdfDocument document;
        public uint PageCount { get { return document == null ? 0 : document.PageCount; } }
        // System WinMetadata is available with Windows itself; avoid requiring a Windows SDK union metadata assembly.
        private static async Task<T> Complete<T>(IAsyncOperation<T> operation)
        {
            try { while (operation.Status == AsyncStatus.Started) await Task.Delay(15); return operation.GetResults(); }
            finally { operation.Close(); }
        }
        private static async Task Complete(IAsyncAction operation)
        {
            try { while (operation.Status == AsyncStatus.Started) await Task.Delay(15); operation.GetResults(); }
            finally { operation.Close(); }
        }
        public async Task Load(string path) { var file = await Complete(StorageFile.GetFileFromPathAsync(path)); document = await Complete(PdfDocument.LoadFromFileAsync(file)); }
        public async Task<BitmapSource> Page(uint number)
        {
            if (document == null || number >= document.PageCount) throw new ArgumentOutOfRangeException("number");
            using (var page = document.GetPage(number))
            using (var stream = new InMemoryRandomAccessStream())
            {
                await Complete(page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = 1100 }));
                using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                {
                    await Complete(reader.LoadAsync((uint)stream.Size)); var bytes = new byte[stream.Size]; reader.ReadBytes(bytes);
                    using (var input = new MemoryStream(bytes)) { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze(); return bitmap; }
                }
            }
        }
    }
}
