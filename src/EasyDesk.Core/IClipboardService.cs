namespace EasyDesk.Core
{
    /// <summary>
    /// Text clipboard operations.
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
    }
}
