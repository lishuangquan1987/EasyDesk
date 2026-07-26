namespace EasyDesk.Core
{
    /// <summary>
    /// Text and file clipboard operations.
    /// NOT thread-safe — clipboard operations must be serialized on a STA thread.
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>
        /// Get Unicode text from the clipboard.
        /// Must be called from a STA thread.
        /// </summary>
        string GetText();

        /// <summary>
        /// Set Unicode text to the clipboard.
        /// Must be called from a STA thread.
        /// </summary>
        void SetText(string text);

        /// <summary>
        /// Check whether the clipboard contains Unicode text.
        /// Must be called from a STA thread.
        /// </summary>
        bool ContainsText();

        /// <summary>
        /// Check whether the clipboard contains files (CF_HDROP format).
        /// Must be called from a STA thread.
        /// </summary>
        bool ContainsFiles();

        /// <summary>
        /// Get the list of file paths currently on the clipboard.
        /// Returns null if clipboard does not contain files.
        /// Must be called from a STA thread.
        /// </summary>
        string[] GetFileList();

        /// <summary>
        /// Set file paths to the clipboard (CF_HDROP format).
        /// All paths must be absolute and exist on the local filesystem.
        /// Must be called from a STA thread.
        /// </summary>
        void SetFiles(string[] filePaths);

        /// <summary>
        /// Check whether the clipboard contains an image (CF_DIB format).
        /// Must be called from a STA thread.
        /// </summary>
        bool ContainsImage();

        /// <summary>
        /// Get the raw CF_DIB bytes from the clipboard.
        /// Returns null if clipboard does not contain an image.
        /// Format: BITMAPINFOHEADER + optional color table + pixel data.
        /// Must be called from a STA thread.
        /// </summary>
        byte[] GetImageDibBytes();

        /// <summary>
        /// Set CF_DIB raw bytes to the clipboard.
        /// The dibBytes must be a valid CF_DIB data (BITMAPINFOHEADER + pixel data).
        /// Must be called from a STA thread.
        /// </summary>
        void SetImageDibBytes(byte[] dibBytes);
    }
}
