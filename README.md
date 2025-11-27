# Baubit.Mediation


[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Mediation/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Mediation)<br/>
[![NuGet](https://img.shields.io/nuget/v/Baubit.Mediation.svg)](https://www.nuget.org/packages/Baubit.Mediation/)
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)<br/>
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Known Vulnerabilities](https://snyk.io/test/github/pnagoorkar/Baubit.Mediation/badge.svg)](https://snyk.io/test/github/pnagoorkar/Baubit.Mediation)


Lightweight mediator pattern implementation with cache-backed async request/response routing.

## Installation

```
dotnet add package Baubit.Mediation
```

## Quick Start

```csharp
using Baubit.Mediation;
using Baubit.Caching;
using Baubit.Caching.InMemory;
using Microsoft.Extensions.Logging;

// Create dependencies
var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var store = new Store<object>(loggerFactory);
var metadata = new Metadata();
var cache = new OrderedCache<object>(new Configuration(), null, store, metadata, loggerFactory);

// Create mediator
var mediator = new Mediator(cache, loggerFactory);

// Define request/response
public class GetUserRequest : IRequest<GetUserResponse>
{
    public int UserId { get; set; }
}

public class GetUserResponse : IResponse
{
    public string Name { get; set; }
}

// Define handler
public class GetUserHandler : IRequestHandler<GetUserRequest, GetUserResponse>
{
    public GetUserResponse Handle(GetUserRequest request)
    {
        return new GetUserResponse { Name = $"User {request.UserId}" };
    }

    public Task<GetUserResponse> HandleSyncAsync(GetUserRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(Handle(request));
    }

    public void Dispose() { }
}

// Register handler and publish request
using var cts = new CancellationTokenSource();
mediator.Subscribe<GetUserRequest, GetUserResponse>(new GetUserHandler(), cts.Token);

var response = mediator.Publish<GetUserRequest, GetUserResponse>(new GetUserRequest { UserId = 1 });
Console.WriteLine(response.Name); // "User 1"
```

## Features

- Synchronous and asynchronous request/response handling
- Cache-backed async processing pipeline
- Notification pub/sub with typed subscribers
- Handler registration with cancellation token lifecycle
- Thread-safe concurrent access

## API Reference

### IMediator

| Method | Description |
|--------|-------------|
| `Publish(object)` | Publish a notification to subscribers |
| `Publish<TRequest, TResponse>(request)` | Synchronous request/response |
| `PublishAsync<TRequest, TResponse>(request)` | Async wrapper for sync handlers |
| `PublishAsyncAsync<TRequest, TResponse>(request)` | Full async with cache-backed tracking |
| `Subscribe<TRequest, TResponse>(handler, ct)` | Register sync handler |
| `SubscribeAsync<TRequest, TResponse>(handler, ct)` | Register async handler |
| `SubscribeAsync<T>(subscriber, ct)` | Subscribe to notifications |

### Handler Interfaces

- `IRequestHandler<TRequest, TResponse>` - Synchronous handler
- `IAsyncRequestHandler<TRequest, TResponse>` - Asynchronous handler
- `ISubscriber<T>` - Notification subscriber

## Dependencies

- [Baubit.Caching](https://www.nuget.org/packages/Baubit.Caching/) - Ordered cache for message persistence

## License

[MIT](LICENSE)