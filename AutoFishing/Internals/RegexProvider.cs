#if NET9_0_OR_GREATER
#    define SUPPORT_GENERATED_REGEX_PROPERTY
#endif  // NET9_0_OR_GREATER
#if NET7_0_OR_GREATER
#    define SUPPORT_GENERATED_REGEX
#endif  // NET7_0_OR_GREATER

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;


namespace AutoFishing.Internals
{
    /// <summary>
    /// Provides some <see cref="Regex"/> instances.
    /// </summary>
#if SUPPORT_GENERATED_REGEX
    internal static partial class RegexProvider
#else
    internal static class RegexProvider
#endif  // SUPPORT_GENERATED_REGEX
    {
        /// <summary>
        /// Options for <see cref="Regex"/> instances.
        /// </summary>
        private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;

        /// <summary>
        /// <see cref="Regex"/> pattern <see cref="string"/> to detect pickup object log.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        // internal const string PickupObjectPattern = @"^Pickup object: '([^']+)' equipped = (True|False), is equippable = (True|False), last input method = (.+), is auto equip controller = (True|False)$";
        internal const string PickupObjectPattern = @"^Pickup object: '([^']+)' equipped = (True|False), is AutoEquipType Pickup = (True|False), last input method = (.+), is AutoHold is enabled for this controller type = (True|False)$";
        /// <summary>
        /// <see cref="Regex"/> pattern <see cref="string"/> to detect drop object log.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        internal const string DropObjectPattern = @"^Drop object: '([^']+), was equipped = (True|False)' (.+), last input method = (.+)$";


        /// <summary>
        /// <see cref="Regex"/> instance to detect pickup object log.
        /// </summary>
#if SUPPORT_GENERATED_REGEX_PROPERTY
        [GeneratedRegex(PickupObjectPattern, Options)]
        public static partial Regex PickupObjectRegex { get; }
#elif SUPPORT_GENERATED_REGEX
        public static Regex PickupObjectRegex => GetPickupObjectRegex();
        /// <summary>
        /// Get <see cref="Regex"/> instance to detect pickup object log.
        /// </summary>
        /// <returns><see cref="Regex"/> instance to detect pickup object log.</returns>
        [GeneratedRegex(PickupObjectPattern, Options)]
        private static partial Regex GetPickupObjectRegex();
#else
        public static Regex PickupObjectRegex => field ??= new Regex(PickupObjectPattern, Options);
#endif  // SUPPORT_GENERATED_REGEX_PROPERTY

        /// <summary>
        /// <see cref="Regex"/> instance to detect drop object log.
        /// </summary>
#if SUPPORT_GENERATED_REGEX_PROPERTY
        [GeneratedRegex(DropObjectPattern, Options)]
        public static partial Regex DropObjectRegex { get; }
#elif SUPPORT_GENERATED_REGEX
        public static Regex DropObjectRegex => GetDropObjectRegex();
        /// <summary>
        /// Get <see cref="Regex"/> instance to detect drop object log.
        /// </summary>
        /// <returns><see cref="Regex"/> instance to detect drop object log.</returns>
        [GeneratedRegex(DropObjectPattern, Options)]
        private static partial Regex GetDropObjectRegex();
#else
        public static Regex DropObjectRegex => field ??= new Regex(DropObjectPattern, Options);
#endif  // SUPPORT_GENERATED_REGEX_PROPERTY
    }
}
