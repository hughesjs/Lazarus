# Lazarus

[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/hughesjs/Lazarus/cd-pipeline.yml?label=BUILD%20CD&style=for-the-badge&branch=master)](https://github.com/hughesjs/Lazarus/actions)
[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/Lazarus?style=for-the-badge)](https://nuget.org/packages/Lazarus)
![GitHub top language](https://img.shields.io/github/languages/top/hughesjs/Lazarus?style=for-the-badge)
[![GitHub](https://img.shields.io/github/license/hughesjs/Lazarus?style=for-the-badge)](LICENSE)
![FTB](https://raw.githubusercontent.com/hughesjs/custom-badges/master/made-in/made-in-scotland.svg)

A bulletproof BackgroundService implementation with auto-resurrection and heartbeat monitoring.

## Overview

Lazarus wraps your background services in a resilient `BackgroundService` implementation that never dies. When exceptions occur, they're logged and the service continues running - no crashes, no restarts needed.

## Features

- **Exception isolation**: Exceptions in your service loop are caught, logged, and the loop continues
- **Heartbeat monitoring**: Built-in watchdog tracks service health via heartbeats
- **Configurable loop delay**: Control the interval between service iterations
- **Clean shutdown**: Proper cancellation token handling for graceful termination

## Installation

```bash
dotnet add package Lazarus
```

## Usage

### 1. Implement `IResilientService`

```csharp
public class MyBackgroundWorker : IResilientService
{
    public string Name => "MyBackgroundWorker";

    public async Task PerformLoop(CancellationToken cancellationToken)
    {
        // Your work here - exceptions won't kill the service
        await ProcessQueuedItems(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        // Cleanup resources
        return ValueTask.CompletedTask;
    }
}
```

### 2. Register with DI

```csharp
services.AddLazarusService<MyBackgroundWorker>(loopDelay: TimeSpan.FromSeconds(5));
```

The `loopDelay` parameter controls how long to wait between each call to `PerformLoop`.

## How It Works

Lazarus wraps your `IResilientService` in a `BackgroundService` that:

1. Calls your `PerformLoop` method in a continuous loop
2. Catches any exceptions thrown and logs them
3. Registers a heartbeat with the watchdog before each iteration
4. Waits for the configured delay before the next iteration
5. Only stops when cancellation is requested

```
┌─────────────────────────────────────────┐
│           LazarusService                │
│  ┌───────────────────────────────────┐  │
│  │  while (!cancelled)               │  │
│  │  {                                │  │
│  │      RegisterHeartbeat();         │  │
│  │      await Delay(loopDelay);      │  │
│  │      try {                        │  │
│  │          await PerformLoop();     │  │
│  │      } catch {                    │  │
│  │          Log.Error();             │  │
│  │          // Continue running      │  │
│  │      }                            │  │
│  │  }                                │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

## Licence

MIT
