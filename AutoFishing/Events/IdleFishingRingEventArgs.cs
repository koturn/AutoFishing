using System;
using AutoFishing.Enums;


namespace AutoFishing.Events
{
    /// <summary>
    /// Provides event data for <see cref="FishingLogWatcher.RingReserved"/>, <see cref="FishingLogWatcher.RingActivated"/>
    /// and <see cref="FishingLogWatcher.RingDeactivated"/>.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Create all members.
    /// </remarks>
    /// <param name="position">Ring posotion.</param>
    /// <param name="kind">Ring kind.</param>
    /// <param name="index">Ring index.</param>
    public sealed class IdleFishingRingEventArgs(int position, RingKind kind, int index) : EventArgs
    {
        /// <summary>
        /// Ring position number.
        /// </summary>
        public int Position { get; } = position;
        /// <summary>
        /// Ring kind.
        /// </summary>
        public RingKind Kind { get; } = kind;
        /// <summary>
        /// Ring index.
        /// </summary>
        public int Index { get; } = index;
        /// <summary>
        /// True if this ring is activated.
        /// </summary>
        public bool IsActivated { get; internal set; } = false;
    }
}
