AutoFishing
===========
[![.NET](https://github.com/koturn/AutoFishing/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/koturn/AutoFishing/actions/workflows/dotnet.yml)

[![Build status](https://ci.appveyor.com/api/projects/status/cwd92e8viklb83ff/branch/main?svg=true)](https://ci.appveyor.com/project/koturn/autofishing/branch/main)

An automation tool for ["A Simple Fishing World"](https://vrchat.com/home/world/wrld_ab93c6a0-d158-4e07-88fe-f8f222018faa/info "A Simple Fishing World") in VRChat.

## Build

```
> git submodule update --init
> nmake
```

## Usage

### 1. Check logging level.

This application reads VRChat log files to operate.
Make sure that logging level is "Full" in VRChat.

[![Logging configuration](https://raw.githubusercontent.com/wiki/koturn/AutoFishing/img/LogConfig.png)](https://raw.githubusercontent.com/wiki/koturn/AutoFishing/img/LogConfig.png "LogConfig.png")

### 2. Check OSC configuration.

This application operates VRChat via OSC.
Make sure that OSC is enabled.

[![OSC configuration](https://raw.githubusercontent.com/wiki/koturn/AutoFishing/img/OSCConfig.png)](https://raw.githubusercontent.com/wiki/koturn/AutoFishing/img/OSCConfig.png "OSCConfig.png")

### 3. Run this application and leave it running.

Go to "A Simple Fishing World", and launch this application.

1. Take out the bucket.
2. Take out the fishing rod and hold it.
3. Adjust the position so the fishing line goes into the bucket.
4. Press the Start button on this application or use the hotkey to start running the application.

See following demo movie.

[![Auto Fishing Demo Movie](http://img.youtube.com/vi/Q7fIAheTJKA/0.jpg)](https://www.youtube.com/watch?v=Q7fIAheTJKA "Auto Fishing Demo - YouTube")

> [!CAUTION]
> This application detects save log.
> When in the waiting state after casting your line, performing actions that trigger a save (such as receiving rewards) causes this application to falsely detect a fish bite.

## LICENSE

This software is released under the MIT License, see [LICENSE](LICENSE "LICENSE").
