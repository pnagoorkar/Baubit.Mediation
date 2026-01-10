# Baubit.Mediation


[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/TpM4QUH8Djox7cjDaNpup5/2zTgJzKbD2m3nXCf5LKvqS/tree/master)
[![codecov](https://codecov.io/gh/pnagoorkar/Baubit.Mediation/branch/master/graph/badge.svg)](https://codecov.io/gh/pnagoorkar/Baubit.Mediation)<br/>
[![NuGet](https://img.shields.io/nuget/v/Baubit.Mediation.svg)](https://www.nuget.org/packages/Baubit.Mediation/)
[![NuGet](https://img.shields.io/nuget/dt/Baubit.Mediation.svg)](https://www.nuget.org/packages/Baubit.Mediation) <br/>
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4?logo=dotnet&logoColor=white)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)<br/>
[![Known Vulnerabilities](https://snyk.io/test/github/pnagoorkar/Baubit.Mediation/badge.svg)](https://snyk.io/test/github/pnagoorkar/Baubit.Mediation)


A lightweight mediator pattern with cache-backed async request/response routing, smoothing out producer backpressure by buffering messages for consumers that process at different rates.<br/><br/>
**DI extension: [Baubit.Mediation.DI](https://github.com/pnagoorkar/Baubit.Mediation.DI)**  
**For persisted mediation: [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB)**   

## Performance

Baubit.Mediation significantly outperforms [MediatR](https://github.com/LuckyPennySoftware/MediatR) in comparable operations across all scenarios:

| Scenario | Baubit.Mediation | MediatR | Outcome |
|----------|------------------|---------|-------------|
| Notification Aggregation | 109 ns / 9.2M ops/sec | 339 ns / 2.9M ops/sec | Baubit is **3.1x faster** ✓ |
| Async Mediation (Request/Response) | 111 ns / 9.0M ops/sec | 425 ns / 2.3M ops/sec | Baubit is **3.9x faster** ✓ |
| Parallel Load (100 ops) | 4,745 ns / 211K ops/sec | 7,986 ns / 125K ops/sec | Baubit is **1.7x faster** ✓ |
| Parallel Load (1000 ops) | 41,241 ns / 24.2K ops/sec | 73,250 ns / 13.7K ops/sec | Baubit is **1.8x faster** ✓ |
| Memory Allocation | 72-240 B per op | 289-361 B per op | Baubit allocates **34-75% less** ✓ |

For detailed benchmark results and methodology, see [Benchmark Results](Baubit.Mediation.Benchmark/results.md).

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

// Define handler
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

var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(new GetUserRequest { UserId = 1 });
Console.WriteLine(response.Name); // "User 1"
```

## Features

- Synchronous and asynchronous request/response handling
- Cache-backed async processing pipeline
- Notification pub/sub with typed subscribers
- **Notification aggregation with and without caching**
  - **With caching (buffering)**: Notifications are persisted to cache before delivery, enabling message replay and distributed pub/sub
  - **Without caching (direct)**: Notifications bypass cache for low-latency direct delivery to subscribers
- Single handler per request type enforcement
- Handler registration with cancellation token lifecycle
- Thread-safe concurrent access
- Optional named cache enumerators for advanced scenarios

## API Reference

### IMediator

| Method | Description |
|--------|-------------|
| `Publish<T>(notification)` | Publish a notification to subscribers synchronously |
| `PublishAsync<T>(notification, ct)` | Publish a notification asynchronously (fire-and-forget) |
| `PublishAsync<TRequest, TResponse>(request, name?, ct)` | Async request/response with optional named cache enumerator |
| `Subscribe<TRequest, TResponse>(handler, enableBuffering, ct)` | Register sync handler |
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

### Notification Aggregation with Caching (Buffering)

When `enableBuffering` is `true` (default), notifications are persisted to the cache before delivery. This enables message replay, durability, and distributed pub/sub capabilities backed by [Baubit.Caching](https://github.com/pnagoorkar/Baubit.Caching/).

```csharp
// Define notification type
public class OrderCreated
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}

// Define subscriber
public class OrderNotificationSubscriber : ISubscriber<OrderCreated>
{
    public bool OnNext(OrderCreated notification)
    {
        Console.WriteLine($"Order {notification.OrderId} created: ${notification.Amount}");
        return true;
    }

    public bool OnError(Exception error)
    {
        Console.WriteLine($"Error: {error.Message}");
        return true;
    }

    public bool OnCompleted() => true;
    public void Dispose() { }
}

// Setup mediator
var cache = new OrderedCache<object>(new Configuration(), null, store, metadata, loggerFactory);
var mediator = new Mediator(cache, loggerFactory);
using var cts = new CancellationTokenSource();

// Subscribe with buffering enabled (default)
var subscriber = new OrderNotificationSubscriber();
var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: true, null, cts.Token);

// Publish notification - stored in cache then delivered
mediator.Publish(new OrderCreated { OrderId = 1, Amount = 99.99m });

// Notifications persist in cache for replay or distributed scenarios
Console.WriteLine($"Cached notifications: {cache.Count}");
```

### Notification Aggregation without Caching (Direct Delivery)

When `enableBuffering` is `false`, notifications bypass the cache and are delivered directly to subscribers. This provides minimal latency for scenarios where persistence is not required.

```csharp
// Same notification and subscriber types as above

// Subscribe with buffering disabled
var subscriber = new OrderNotificationSubscriber();
var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: false, null, cts.Token);

// Publish notification - delivered directly without caching
mediator.Publish(new OrderCreated { OrderId = 2, Amount = 149.99m });

// No caching overhead - immediate delivery
Console.WriteLine($"Cached notifications: {cache.Count}"); // 0
```

### Mixed Buffering Scenarios

Different subscribers can use different buffering strategies for the same notification type:

```csharp
var bufferedSubscriber = new OrderNotificationSubscriber();
var directSubscriber = new OrderNotificationSubscriber();

// One subscriber with caching, one without
var bufferedTask = mediator.SubscribeAsync(bufferedSubscriber, enableBuffering: true, null, cts.Token);
var directTask = mediator.SubscribeAsync(directSubscriber, enableBuffering: false, null, cts.Token);

// Publish once - buffered subscriber gets it from cache, direct subscriber gets immediate delivery
mediator.Publish(new OrderCreated { OrderId = 3, Amount = 199.99m });

// Both subscribers receive the notification via their preferred delivery mechanism
```

### Request/Response Mediation

```csharp
// Async request/response for sync handlers
var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(new GetUserRequest { UserId = 1 });

// Async request/response with named cache enumerator
var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 1 }, 
    "my-handler",
    CancellationToken.None);
```

### Function-Based Subscriptions

For scenarios where creating a full handler class is unnecessary, use function-based subscriptions:

#### Notification Handler Functions

```csharp
// Subscribe to notifications using a function handler with buffering
using var cts = new CancellationTokenSource();
var subscribeTask = mediator.SubscribeAsync<OrderCreated>(
    async (notification, ct) =>
    {
        Console.WriteLine($"Order {notification.OrderId} received");
        await ProcessOrderAsync(notification, ct);
        return true;
    },
    enableBuffering: true,
    null,
    cts.Token
);

// Publish notifications - function handler receives them from cache
mediator.Publish(new OrderCreated { OrderId = 1, Amount = 99.99m });
mediator.Publish(new OrderCreated { OrderId = 2, Amount = 149.99m });

// Cancel subscription when done
cts.Cancel();
```

#### Async Request Handler Functions

```csharp
// Subscribe to requests using a function handler
using var cts = new CancellationTokenSource();
var subscribeTask = mediator.SubscribeAsync<GetUserRequest, GetUserResponse>(
    async (request, ct) =>
    {
        var user = await database.GetUserAsync(request.UserId, ct);
        return new GetUserResponse { Name = user.Name };
    },
    enableBuffering: true,
    null,
    cts.Token
);

// Publish async request - function handler processes it
var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 1 },
    null,
    CancellationToken.None
);

// Cancel subscription when done
cts.Cancel();
```

## Dependencies

- [Baubit.Caching](https://github.com/pnagoorkar/Baubit.Caching/) v2026.2.2-prerelease or later

## Architecture Notes

**MediatR vs Baubit.Mediation**:
- **MediatR**: Offers built-in pipeline behaviors optimized for in-memory processing
- **Baubit.Mediation**: Expects pipelines to be built outside of its knowledge, focusing on cache-backed durability and distributed messaging

**Cache-Backed Async Mediation**:

Baubit.Mediation is powered by [Baubit.Caching](https://github.com/pnagoorkar/Baubit.Caching/), a high-performance hybrid cache. Baubit.Caching is being extended to support distributed systems - once complete, Baubit.Mediation will natively support distributed mediation scenarios.

## License


[MIT](LICENSE)



