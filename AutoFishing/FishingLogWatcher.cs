using System;
using System.Collections.Generic;
using Koturn.VRChat.Log;
using Koturn.VRChat.Log.Enums;
using Koturn.VRChat.Log.Events;
using AutoFishing.Internals;
using AutoFishing.Events;
using AutoFishing.Enums;


namespace AutoFishing
{
    /// <summary>
    /// Output all logs to stdout.
    /// </summary>
    public sealed class FishingLogWatcher : VRCBaseLogWatcher
    {
        /// <summary>
        /// Occurs when detect a log that you joined to instance.
        /// </summary>
        public event VRCLogEventHandler<InstanceEventArgs>? JoinedToInstance;
        /// <summary>
        /// Occurs when detect a log that you left from instance.
        /// </summary>
        public event VRCLogEventHandler<InstanceEventArgs>? LeftFromInstance;
        /// <summary>
        /// Occurs when detect SAVE log.
        /// </summary>
        /// <remarks>
        /// For "A Simple Fishing World".
        /// </remarks>
        public event EventHandler? DataSaved;
        /// <summary>
        /// Occurs when fish pickuped.
        /// </summary>
        public event EventHandler? FishPickuped;
        /// <summary>
        /// <para>Occurs when fish pickuped.</para>
        /// </summary>
        public event EventHandler? ReelingStarted;
        /// <summary>
        /// Occurs when a rod picked up.
        /// </summary>
        public event VRCLogEventHandler<ObjectPickedupEventArgs>? RodPickedUp;
        /// <summary>
        /// Occurs when a rod dropped.
        /// </summary>
        public event VRCLogEventHandler<ObjectDroppedEventArgs>? RodDropped;
        /// <summary>
        /// Occurs when new ring is reserved.
        /// </summary>
        /// <remarks>
        /// For "Idle Fishing".
        /// </remarks>
        public event VRCLogEventHandler<IdleFishingRingEventArgs>? RingReserved;
        /// <summary>
        /// Occurs when a ring is activated.
        /// </summary>
        /// <remarks>
        /// For "Idle Fishing".
        /// </remarks>
        public event VRCLogEventHandler<IdleFishingRingEventArgs>? RingActivated;
        /// <summary>
        /// Occurs when a ring is deactivated.
        /// </summary>
        /// <remarks>
        /// For "Idle Fishing".
        /// </remarks>
        public event VRCLogEventHandler<IdleFishingRingEventArgs>? RingDeactivated;
        /// <summary>
        /// Current instance information.
        /// </summary>
        /// <remarks>Running multiple instances of the VRChat client simultaneously is not supported.</remarks>
        public InstanceInfo? InstanceInfo { get; private set; }


        /// <summary>
        /// Create an instance of <see cref="VRCBaseLogParser"/>.
        /// </summary>
        /// <param name="filePath">File path to log file.</param>
        /// <returns>An instance of <see cref="VRCBaseLogParser"/>.</returns>
        protected override VRCBaseLogParser CreateLogParser(string filePath)
        {
            return new FishingLogParser(this, filePath);
        }

        /// <summary>
        /// <see cref="VRCBaseLogParser"/> for <see cref="FishingLogWatcher"/>.
        /// </summary>
        /// <remarks>
        /// Primary ctor: Open specified file.
        /// </remarks>
        /// <param name="watcher">Parent <see cref="FishingLogWatcher"/>.</param>
        /// <param name="filePath">Log file path to open.</param>
        private sealed class FishingLogParser(FishingLogWatcher watcher, string filePath) : VRCBaseLogParser(filePath)
        {
            /// <summary>
            /// "[Behaviour]" log offset.
            /// </summary>
            private const int BehaviourLogOffset = 12;
            /// <summary>
            /// Internal object name of the rod in "A Simple Fishing World".
            /// </summary>
            private const string SimpleFishingWorldRodName = "Rod Pickup";
            /// <summary>
            /// Internal object name of the rod in "Idle Fishing".
            /// </summary>
            private const string IdleFishingRodName = "Fishing Rod Pickup";

            /// <summary>
            /// Instance information.
            /// </summary>
            private InstanceInfo _instanceInfo = new(default);
            /// <summary>
            /// Function pointer to a function that parses the log for a specific world.
            /// </summary>
            private unsafe delegate*<FishingLogParser, FishingLogWatcher, string, bool> _parseAsSpecificWorldLog;
            /// <summary>
            /// Internal object name of the rod in the current world.
            /// </summary>
            private string? _rodName;
            /// <summary>
            /// Array of the <see cref="IdleFishingRingEventArgs"/>.
            /// </summary>
            /// <remarks>This array has two elements only.</remarks>
            private readonly IdleFishingRingEventArgs?[] _idleFishingRingEventArgArray = new IdleFishingRingEventArgs[2];


            /// <summary>
            /// Load one log item and output to stdout.
            /// </summary>
            /// <param name="level">Log level.</param>
            /// <param name="logLines">Log lines.</param>
            /// <returns>True if any of the log parsing defined in this class succeeds, otherwise false.</returns>
            protected override bool OnLogDetected(VRCLogLevel level, List<string> logLines)
            {
                var firstLine = logLines[0];

                if (ParseAsBehaviourLog(firstLine))
                {
                    return true;
                }

                unsafe
                {
                    if (_parseAsSpecificWorldLog != null && _parseAsSpecificWorldLog(this, watcher, firstLine))
                    {
                        return true;
                    }
                }

                return false;
            }


            /// <summary>
            /// Parse first log line as "[Behaviour]" log.
            /// </summary>
            /// <param name="firstLine">First log line.</param>
            /// <returns>True if parsed successfully, false otherwise.</returns>
            /// <exception cref="Koturn.VRChat.Log.Exceptions.InvalidLogException">Thrown when duplicate joined timestamp.</exception>
            private bool ParseAsBehaviourLog(string firstLine)
            {
                if (!firstLine.StartsWith("[Behaviour] ", StringComparison.Ordinal))
                {
                    return false;
                }

                return ParseAsPickupObjectLog(firstLine)
                    || ParseAsDropObjectLog(firstLine)
                    || ParseAsJoiningLog(firstLine)
                    || ParseAsLeftLog(firstLine);
            }

            /// <summary>
            /// Parse first log line as pickup object log.
            /// </summary>
            /// <param name="firstLine">First log line.</param>
            /// <returns>True if parsed successfully, false otherwise.</returns>
            private bool ParseAsPickupObjectLog(string firstLine)
            {
                var match = RegexProvider.PickupObjectRegex.Match(firstLine, BehaviourLogOffset, firstLine.Length - BehaviourLogOffset);
                if (!match.Success)
                {
                    return false;
                }

                var groups = match.Groups;
                var objectName = groups[1].Value;
                if (objectName == _rodName)
                {
                    watcher.RodPickedUp?.Invoke(
                        this,
                        new ObjectPickedupEventArgs(
                            FileName,
                            LogUntil,
                            objectName,
                            bool.Parse(groups[2].Value),
                            bool.Parse(groups[3].Value),
                            groups[4].Value,
                            bool.Parse(groups[5].Value)));
                }

                return true;
            }

            /// <summary>
            /// Parse first log line as drop object log.
            /// </summary>
            /// <param name="firstLine">First log line.</param>
            /// <returns>True if parsed successfully, false otherwise.</returns>
            private bool ParseAsDropObjectLog(string firstLine)
            {
                var match = RegexProvider.DropObjectRegex.Match(firstLine, BehaviourLogOffset, firstLine.Length - BehaviourLogOffset);
                if (!match.Success)
                {
                    return false;
                }

                var groups = match.Groups;
                var objectName = groups[1].Value;
                if (objectName == _rodName)
                {
                    watcher.RodDropped?.Invoke(
                        this,
                        new ObjectDroppedEventArgs(
                            FileName,
                            LogUntil,
                            objectName,
                            bool.Parse(groups[2].Value),
                            groups[3].Value,
                            groups[4].Value));
                }

                return true;
            }

            /// <summary>
            /// Parse first log line as joining to instance log.
            /// </summary>
            /// <param name="firstLine">First log line.</param>
            /// <returns>True if parsed successfully, false otherwise.</returns>
            private bool ParseAsJoiningLog(string firstLine)
            {
                if (!IsSubstringAt("Joining ", firstLine, BehaviourLogOffset))
                {
                    return false;
                }

                const int instanceStringOffset = BehaviourLogOffset + 8;
                if (IsSubstringAt("or Creating Room: ", firstLine, instanceStringOffset))
                {
                    _instanceInfo.WorldName = firstLine.Substring(BehaviourLogOffset + 26);
                    watcher.JoinedToInstance?.Invoke(this, new InstanceEventArgs(FileName, LogUntil, _instanceInfo));

                    unsafe
                    {
                        switch (_instanceInfo.WorldId)
                        {
                            case WorldIds.SimpleFishingWorld:
                                _parseAsSpecificWorldLog = &ParseAsSimpleFishingWorldLog;
                                _rodName = SimpleFishingWorldRodName;
                                break;
                            case WorldIds.IdleFishing:
                                _parseAsSpecificWorldLog = &ParseAsIdleFishingLog;
                                _rodName = IdleFishingRodName;
                                break;
                            default:
                                _parseAsSpecificWorldLog = null;
                                _rodName = null;
                                break;
                        }
                    }

                    return true;
                }
                else if (IsSubstringAt("wrld_", firstLine, instanceStringOffset))
                {
                    _instanceInfo = ParseInstanceString(firstLine.Substring(instanceStringOffset), LogUntil);
                    watcher.InstanceInfo = _instanceInfo;
                    return true;
                }
                else if (IsSubstringAt("local:", firstLine, instanceStringOffset))
                {
                    _instanceInfo = ParseLocalInstanceString(firstLine.Substring(instanceStringOffset), LogUntil);
                    watcher.InstanceInfo = _instanceInfo;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Parse first log line as left from instance log.
            /// </summary>
            /// <param name="firstLine">First log line.</param>
            /// <returns>True if parsed successfully, false otherwise.</returns>
            private bool ParseAsLeftLog(string firstLine)
            {
                if (!IsSubstringAt("OnLeftRoom", firstLine, BehaviourLogOffset))
                {
                    return false;
                }

                _instanceInfo.StayUntil = LogUntil;
                watcher.LeftFromInstance?.Invoke(this, new InstanceEventArgs(FileName, LogUntil, _instanceInfo));
                _instanceInfo.IsEmitted = true;

                return true;
            }

            /// <summary>
            /// Parse instance string.
            /// </summary>
            /// <param name="instanceString">Instance string.</param>
            /// <param name="stayFrom">Log timestamp.</param>
            /// <returns>Parsed result.</returns>
            /// <exception cref="Koturn.VRChat.Log.Exceptions.InvalidLogException">Thrown when mismatch parent detected.</exception>
            private InstanceInfo ParseInstanceString(string instanceString, DateTime stayFrom)
            {
                var tokens = instanceString.Split('~');
                var ids = tokens[0].Split(':');

                var instanceInfo = new InstanceInfo(stayFrom)
                {
                    WorldId = ids[0],
                    InstanceString = instanceString,
                    InstanceId = ids[1],
                    InstanceType = InstanceType.Public,
                    LogFrom = LogFrom
                };

                // Options
                var canRequestInvite = false;
                for (int i = 1; i < tokens.Length; i++)
                {
                    var token = tokens[i];
                    var (optName, optArg) = ParseInstanceStringOption(token);
                    switch (optName)
                    {
                        case "canRequestInvite":
                            canRequestInvite = true;
                            if (instanceInfo.InstanceType == InstanceType.Invite)
                            {
                                instanceInfo.InstanceType = InstanceType.InvitePlus;
                            }
                            break;
                        case "public":
                            instanceInfo.InstanceType = InstanceType.Public;
                            instanceInfo.UserOrGroupId = optArg;
                            break;
                        case "hidden":
                            instanceInfo.InstanceType = InstanceType.FriendPlus;
                            instanceInfo.UserOrGroupId = optArg;
                            break;
                        case "friends":
                            instanceInfo.InstanceType = InstanceType.Friend;
                            instanceInfo.UserOrGroupId = optArg;
                            break;
                        case "private":
                            instanceInfo.InstanceType = canRequestInvite ? InstanceType.InvitePlus : InstanceType.Invite;
                            instanceInfo.UserOrGroupId = optArg;
                            break;
                        case "region":
                            instanceInfo.Region = optArg switch
                            {
                                "us" => Region.USW,
                                "use" => Region.USE,
                                "eu" => Region.EU,
                                "jp" => Region.JP,
                                _ => throw CreateInvalidLogException($"Unrecognized region is detected: {optArg}")
                            }; ;
                            break;
                        case "nonce":
                            instanceInfo.Nonce = optArg;
                            break;
                        case "group":
                            instanceInfo.UserOrGroupId = optArg;
                            break;
                        case "groupAccessType":
                            instanceInfo.InstanceType = optArg switch
                            {
                                "public" => InstanceType.GroupPublic,
                                "plus" => InstanceType.GroupPlus,
                                "members" => InstanceType.GroupMembers,
                                _ => instanceInfo.InstanceType
                            };
                            break;
                        default:
                            ThrowInvalidLogException($"Unknown option detected: {token}");
                            break;
                    }
                }

                return instanceInfo;
            }

            /// <summary>
            /// Parse local instance string.
            /// </summary>
            /// <param name="instanceString">Instance string.</param>
            /// <param name="stayFrom">Log timestamp.</param>
            /// <returns>Parsed result.</returns>
            private InstanceInfo ParseLocalInstanceString(string instanceString, DateTime stayFrom)
            {
                var ids = instanceString.Split(':');
                return new InstanceInfo(stayFrom)
                {
                    WorldId = ids[0],
                    InstanceString = instanceString,
                    InstanceId = ids[1],
                    InstanceType = InstanceType.Public,  // VRChat says "Public".
                    Region = Region.LocalTest,
                    LogFrom = LogFrom
                };
            }

            /// <summary>
            /// Parse instance string optipn, "~XXX(YYY)".
            /// </summary>
            /// <param name="option">Option string.</param>
            /// <returns>Parsed result, tuple of optioh name and arguments.</returns>
            /// <exception cref="Koturn.VRChat.Log.Exceptions.InvalidLogException">Thrown when mismatch parent detected.</exception>
            private (string OptionName, string? OptionArg) ParseInstanceStringOption(string option)
            {
                var idxParenStart = option.IndexOf('(');
                if (idxParenStart == -1)
                {
                    return (option, null);
                }

                var idxParenEnd = option.IndexOf(')', idxParenStart + 1);
                if (idxParenStart == -1)
                {
                    ThrowInvalidLogException($"Corresponding parens in instance string are not found: {option}");
                }

                return (option.Substring(0, idxParenStart), option.Substring(idxParenStart + 1, idxParenEnd - idxParenStart - 1));
            }


            /// <summary>
            /// Parse log lines as "A Simple Fishing World" log.
            /// </summary>
            /// <param name="parser"><see cref="FishingLogParser"/> instance.</param>
            /// <param name="watcher">Log watcher.</param>
            /// <param name="firstLine">First log line.</param>
            /// <returns>True if parsed successfully, false otherwise.</returns>
            private static bool ParseAsSimpleFishingWorldLog(FishingLogParser parser, FishingLogWatcher watcher, string firstLine)
            {
                if (firstLine == "SAVED DATA")
                {
                    watcher.DataSaved?.Invoke(parser, EventArgs.Empty);
                    return true;
                }
                else if (firstLine == "Fish Pickup attached to rod Toggles(True)")
                {
                    watcher.FishPickuped?.Invoke(parser, EventArgs.Empty);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Parse log lines as "Idle Fishing" log.
            /// </summary>
            /// <param name="parser"><see cref="VRCCoreExLogParser"/> instance.</param>
            /// <param name="watcher">Log watcher.</param>
            /// <param name="firstLine">First log line.</param>
            /// <returns>True if parsed successfully, false otherwise.</returns>
            private static bool ParseAsIdleFishingLog(FishingLogParser parser, FishingLogWatcher watcher, string firstLine)
            {
                if (firstLine == "Started Reeling")
                {
                    watcher.ReelingStarted?.Invoke(parser, EventArgs.Empty);
                    return true;
                }
                else if (firstLine.StartsWith("Reserving ring ", StringComparison.Ordinal))
                {
#if NET9_0_OR_GREATER
                    var tokenSpan = firstLine.AsSpan(15);
                    var numberTokenEnumerator = tokenSpan.Split(' ');

                    numberTokenEnumerator.MoveNext();
                    var ringPosition = int.Parse(tokenSpan[numberTokenEnumerator.Current]);

                    numberTokenEnumerator.MoveNext();
                    var ringKind = (RingKind)int.Parse(tokenSpan[numberTokenEnumerator.Current]);

                    numberTokenEnumerator.MoveNext();
                    var ringIndex = int.Parse(tokenSpan[numberTokenEnumerator.Current]);
#else
                    var numberTokens = firstLine.Substring(15).Split(' ');
                    var ringPosition = int.Parse(numberTokens[0]);
                    var ringKind = (RingKind)int.Parse(numberTokens[1]);
                    var ringIndex = int.Parse(numberTokens[2]);
#endif  // NET9_0_OR_GREATER

                    var idleFisingEventArgs = new IdleFishingRingEventArgs(ringPosition, ringKind, ringIndex);
                    parser._idleFishingRingEventArgArray[ringIndex] = idleFisingEventArgs;
                    watcher.RingReserved?.Invoke(watcher, idleFisingEventArgs);

                    return true;
                }
                else if (firstLine.StartsWith("Activate Ring ", StringComparison.Ordinal))
                {
#if NET7_0_OR_GREATER
                    if (int.TryParse(firstLine.AsSpan(14), out int ringIndex))
#else
                    if (int.TryParse(firstLine.Substring(14), out int ringIndex))
#endif  // NET7_0_OR_GREATER
                    {
                        var e = parser._idleFishingRingEventArgArray[ringIndex];
                        if (e != null)
                        {
                            e.IsActivated = true;
                            watcher.RingActivated?.Invoke(watcher, e);
                        }
                    }

                    return true;
                }
                else if (firstLine.StartsWith("Deactivate Ring ", StringComparison.Ordinal))
                {
#if NET7_0_OR_GREATER
                    if (int.TryParse(firstLine.AsSpan(16), out int ringIndex))
#else
                    if (int.TryParse(firstLine.Substring(16), out int ringIndex))
#endif  // NET7_0_OR_GREATER
                    {
                        var e = parser._idleFishingRingEventArgArray[ringIndex];
                        if (e != null)
                        {
                            watcher.RingDeactivated?.Invoke(watcher, e);
                            parser._idleFishingRingEventArgArray[ringIndex] = null;
                        }
                    }

                    return true;
                }

                return false;
            }
        }
    }
}
