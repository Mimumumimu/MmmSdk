using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MmmSdk.WinUI.Components.Attachments;

/// <summary>画像を JPEG に変換する</summary>
/// <remarks>画像の大きさ（解像度・容量）に上限は設けない。大きな画像も、元の解像度のまま変換する（意図した仕様。上限を付けると、添付したい画像が添付できなくなるため）。</remarks>
public sealed class ImageConverter : IImageConverter
{
    /// <inheritdoc />
    public async Task<byte[]> ToJpegAsync(Stream image)
    {
        var decoder = await BitmapDecoder.CreateAsync(image.AsRandomAccessStream());
        // JPEG は透過を持てないので、アルファは無視して変換する
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

        using var output = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        var bytes = new byte[output.Size];
        using var reader = new DataReader(output.GetInputStreamAt(0));
        await reader.LoadAsync((uint)output.Size);
        reader.ReadBytes(bytes);
        return bytes;
    }
}
