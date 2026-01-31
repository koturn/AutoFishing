#if !NET7_0_OR_GREATER


#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Diagnostics.CodeAnalysis
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Specifies the syntax used in a string.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Initializes the <see cref="StringSyntaxAttribute"/> with the identifier of the syntax used.
    /// </remarks>
    /// <param name="syntax">The syntax identifier.</param>
    /// <param name="arguments">Optional arguments associated with the specific syntax employed.</param>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class StringSyntaxAttribute(string syntax, params object?[] arguments) : Attribute
    {
        /// <summary>
        /// Initializes the <see cref="StringSyntaxAttribute"/> with the identifier of the syntax used.
        /// </summary>
        /// <param name="syntax">The syntax identifier.</param>
        public StringSyntaxAttribute(string syntax)
            : this(syntax, [])
        {
        }

        /// <summary>
        /// Gets the identifier of the syntax used.
        /// </summary>
        public string Syntax { get; } = syntax;
        /// <summary>
        /// Optional arguments associated with the specific syntax employed.
        /// </summary>
        public object?[] Arguments { get; } = arguments;

        /// <summary>
        /// The syntax identifier for strings containing composite formats for string formatting.
        /// </summary>
        public const string CompositeFormat = nameof(CompositeFormat);
        /// <summary>
        /// The syntax identifier for strings containing date format specifiers.
        /// </summary>
        public const string DateOnlyFormat = nameof(DateOnlyFormat);
        /// <summary>
        /// The syntax identifier for strings containing date and time format specifiers.
        /// </summary>
        public const string DateTimeFormat = nameof(DateTimeFormat);
        /// <summary>
        /// The syntax identifier for strings containing <see cref="Enum"/> format specifiers.
        /// </summary>
        public const string EnumFormat = nameof(EnumFormat);
        /// <summary>
        /// The syntax identifier for strings containing <see cref="Guid"/> format specifiers.
        /// </summary>
        public const string GuidFormat = nameof(GuidFormat);
        /// <summary>
        /// The syntax identifier for strings containing JavaScript Object Notation (JSON).
        /// </summary>
        public const string Json = nameof(Json);
        /// <summary>
        /// The syntax identifier for strings containing numeric format specifiers.
        /// </summary>
        public const string NumericFormat = nameof(NumericFormat);
        /// <summary>
        /// The syntax identifier for strings containing regular expressions.
        /// </summary>
        public const string Regex = nameof(Regex);
        /// <summary>
        /// The syntax identifier for strings containing time format specifiers.
        /// </summary>
        public const string TimeOnlyFormat = nameof(TimeOnlyFormat);
        /// <summary>
        /// The syntax identifier for strings containing <see cref="TimeSpan"/> format specifiers.
        /// </summary>
        public const string TimeSpanFormat = nameof(TimeSpanFormat);
        /// <summary>
        /// The syntax identifier for strings containing URIs.
        /// </summary>
        public const string Uri = nameof(Uri);
        /// <summary>
        /// The syntax identifier for strings containing XML.
        /// </summary>
        public const string Xml = nameof(Xml);
    }
}


#endif  // !NET7_0_OR_GREATER
