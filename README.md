# Baubit.Mediation


[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Mediation/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Mediation)<br/>
[![NuGet](https://img.shields.io/nuget/v/Baubit.Mediation.svg)](https://www.nuget.org/packages/Baubit.Mediation/)
[![NuGet](https://img.shields.io/nuget/dt/Baubit.Mediation.svg)](https://www.nuget.org/packages/Baubit.Mediation) <br/>
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)<br/>
[![Known Vulnerabilities](https://snyk.io/test/github/pnagoorkar/Baubit.Mediation/badge.svg)](https://snyk.io/test/github/pnagoorkar/Baubit.Mediation)


A lightweight mediator pattern implementation with cache-backed async request/response routing.

**DI extension: [Baubit.Mediation.DI](https://github.com/pnagoorkar/Baubit.Mediation.DI)**  
**For persisted mediation: [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB)**   

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
long nextId = 0;
Func<long?, long?> nextIdFactory = (lastId) => Interlocked.Increment(ref nextId);
var store = new Store<long, object>(null, null, nextIdFactory, loggerFactory);
var metadata = new Metadata<long>(new Configuration(), loggerFactory);
var cache = new OrderedCache<long, object>(new Configuration(), null, store, metadata, loggerFactory);

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

// Define synchronous handler
public class GetUserHandler : IRequestHandler<GetUserRequest, GetUserResponse>
{
    public GetUserResponse Handle(GetUserRequest request)
    {
        return new GetUserResponse { Name = $"User {request.UserId}" };
    }
}

// Register handler and publish request
using var cts = new CancellationTokenSource();
mediator.Subscribe<GetUserRequest, GetUserResponse>(new GetUserHandler(), true, cts.Token);

var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 1 });
Console.WriteLine(response.Name); // "User 1"
```

## Features

- Synchronous and asynchronous request/response handling
- Cache-backed async processing pipeline
- Notification pub/sub with typed subscribers
- Notification aggregation with buffering options
- Single handler per request type enforcement
- Handler registration with cancellation token lifecycle
- Thread-safe concurrent access

## API Reference

### IMediator

| Method | Description |
|--------|-------------|
| `Publish<T>(notification)` | Publish a notification synchronously |
| `PublishAsync<T>(notification, ct)` | Publish a notification asynchronously (fire-and-forget) |
| `PublishAsync<TRequest, TResponse>(request, name?, ct)` | Async request/response with optional named cache enumerator |
| `Subscribe<TRequest, TResponse>(handler, enableBuffering, ct)` | Register synchronous request handler |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, name?, ct)` | Register async handler (IAsyncRequestHandler) |
| `SubscribeAsync<T>(subscriber, enableBuffering, name?, ct)` | Subscribe to notifications (ISubscriber) |
| `SubscribeAsync<TNotification>(func, enableBuffering, name?, ct)` | Subscribe to notifications using function handler |
| `SubscribeAsync<TRequest, TResponse>(func, enableBuffering, name?, ct)` | Register async handler using function |

### Handler Interfaces

- `IRequestHandler<TRequest, TResponse>` - Synchronous handler with `Handle(TRequest)` method
- `IAsyncRequestHandler<TRequest, TResponse>` - Asynchronous handler with `HandleAsync(TRequest)` method
- `ISubscriber<T>` - Notification subscriber with `OnNext`, `OnError`, `OnCompleted` methods

### Handler Constraints

Only one handler can be registered per request type. Attempts to register a second handler for the same request type will return `false`.

## Usage Examples

### Notification Subscription

```csharp
public class OrderNotificationSubscriber : ISubscriber<OrderCreated>
{
    public bool OnNext(OrderCreated notification)
    {
        Console.WriteLine($"Order {notification.OrderId} created");
        return true;
    }

    public bool OnError(Exception error) => true;
    public bool OnCompleted() => true;
    public void Dispose() { }
}

using var cts = new CancellationTokenSource();
var subscriber = new OrderNotificationSubscriber();

// Subscribe with buffering enabled (notifications stored in cache)
var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: true, null, cts.Token);

// Publish notification
mediator.Publish(new OrderCreated { OrderId = 1 });
```

### Async Request Handler

```csharp
public class AsyncGetUserHandler : IAsyncRequestHandler<GetUserRequest, GetUserResponse>
{
    public async Task<GetUserResponse> HandleAsync(GetUserRequest request)
    {
        await Task.Delay(10); // Simulate async operation
        return new GetUserResponse { Name = $"User {request.UserId}" };
    }
}

using var cts = new CancellationTokenSource();
var subscribeTask = mediator.SubscribeAsync<GetUserRequest, GetUserResponse>(
    new AsyncGetUserHandler(), true, null, cts.Token);

var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 1 });
```

### Function-Based Handlers

```csharp
// Notification handler function
var subscribeTask = mediator.SubscribeAsync<OrderCreated>(
    async (notification, ct) =>
    {
        await ProcessOrderAsync(notification, ct);
        return true;
    },
    true,
    null,
    cts.Token
);

// Request handler function
var subscribeTask = mediator.SubscribeAsync<GetUserRequest, GetUserResponse>(
    async (request, ct) =>
    {
        var user = await database.GetUserAsync(request.UserId, ct);
        return new GetUserResponse { Name = user.Name };
    },
    true,
    null,
    cts.Token
);

var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 1 });
```

## Dependencies

- [Baubit.Caching](https://github.com/pnagoorkar/Baubit.Caching/) v2026.2.2-prerelease or later

## License

[MIT](LICENSE)
