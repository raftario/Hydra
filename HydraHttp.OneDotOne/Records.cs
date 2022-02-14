using System.Diagnostics.CodeAnalysis;

namespace HydraHttp.OneDotOne
{
    /// <summary>
    /// A parsing operation status
    /// </summary>
    public enum Status
    {
        /// <summary>
        /// Parsing of the element complete
        /// </summary>
        Complete,
        /// <summary>
        /// Parsing of the element incomplete
        /// </summary>
        Incomplete,
        /// <summary>
        /// Parsing of the element's section finished
        /// </summary>
        Finished,
    }
    /// <summary>
    /// A parsing operation result
    /// </summary>
    /// <typeparam name="T">Parsed element type</typeparam>
    /// <param name="Status">Parsing status</param>
    /// <param name="Value">Parsed element if the status is <see cref="Status.Complete"/></param>
    public readonly record struct Result<T>(Status Status, T? Value = null) where T : struct
    {
        /// <summary>
        /// Obtains the parsed element if parsing completed
        /// </summary>
        /// <param name="value">Parsed value</param>
        /// <returns>true if parsing of the element completed</returns>
        public bool Complete([NotNullWhen(true)] out T? value)
        {
            value = Value;
            return Status == Status.Complete;
        }
        /// <summary>
        /// Whether the parsing status is <see cref="Status.Incomplete"/>
        /// </summary>
        public bool Incomplete => Status == Status.Incomplete;
        /// <summary>
        /// Whether the parsing status is <see cref="Status.Finished"/>
        /// </summary>
        public bool Finished => Status == Status.Finished;
    }

    /// <summary>
    /// An HTTP request start line
    /// </summary>
    /// <param name="Method">HTTP request method</param>
    /// <param name="Uri">HTTP request URI</param>
    /// <param name="Version">HTTP request protocol minor version</param>
    public readonly record struct StartLine(string Method, string Uri, int Version);
    /// <summary>
    /// An HTTP response status line
    /// </summary>
    /// <param name="Status">HTTP response status</param>
    /// <param name="Reason">Reason phrase</param>
    public readonly record struct StatusLine(int Status, string Reason);
    /// <summary>
    /// An HTTP header
    /// </summary>
    /// <param name="Name">Header name</param>
    /// <param name="Value">Header value</param>
    public readonly record struct Header(string Name, string Value);
}
