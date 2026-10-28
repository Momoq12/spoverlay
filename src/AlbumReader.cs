using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
public static class AlbumReader {
 public static async Task<byte[]> TestMemory() { using(var stream = new InMemoryRandomAccessStream()) { var writer = new DataWriter(stream); writer.WriteBytes(new byte[] { 71,73,70,56,57,97 }); await System.WindowsRuntimeSystemExtensions.AsTask<uint>(writer.StoreAsync()); writer.DetachStream(); writer.Dispose(); stream.Seek(0); return await Read(RandomAccessStreamReference.CreateFromStream(stream)); } }
 public static async Task<byte[]> TestFile(string path) { var file = await System.WindowsRuntimeSystemExtensions.AsTask<Windows.Storage.StorageFile>(Windows.Storage.StorageFile.GetFileFromPathAsync(path)); return await Read(RandomAccessStreamReference.CreateFromFile(file)); }
 public static async Task<byte[]> Read(IRandomAccessStreamReference reference) {
  using(var stream = await System.WindowsRuntimeSystemExtensions.AsTask<IRandomAccessStreamWithContentType>(reference.OpenReadAsync()))
  using(var input = stream.AsStreamForRead())
  using(var buffer = new MemoryStream()) {
   await input.CopyToAsync(buffer);
   return buffer.ToArray();
  }
 }
}




