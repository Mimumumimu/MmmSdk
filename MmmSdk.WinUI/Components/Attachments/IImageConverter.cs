namespace MmmSdk.WinUI.Components.Attachments;

/// <summary>画像の変換</summary>
public interface IImageConverter
{
    /// <summary>画像 (PNG・BMP 等)を JPEG に変換する。</summary>
    /// <param name="image">変換する画像のストリーム</param>
    /// <returns>JPEG のバイト列</returns>
    /// <remarks>画像の大きさに上限は設けない (意図した仕様。元の解像度のまま変換する)。</remarks>
    Task<byte[]> ToJpegAsync(Stream image);
}
