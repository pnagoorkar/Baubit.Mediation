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
using System.Threading;

// Create dependencies
var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var configuration = new Baubit.Caching.Configuration();
Func<long?, long?> nextIdFactory = (lastId) => Interlocked.Increment(ref lastId ?? 0);
var store = new Baubit.Caching.InMemory.Store<long, object>(null, null, nextIdFactory, loggerFactory);
var metadata = new Baubit.Caching.InMemory.Metadata<long>(configuration, loggerFactory);
var cache = new Baubit.Caching.OrderedCache<long, object>(configuration, null, store, metadata, loggerFactory);

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
_ = mediator.SubscribeAsync<GetUserRequest, GetUserResponse>(new GetUserHandler(), true, cts.Token);

var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(new GetUserRequest { UserId = 1 });
Console.WriteLine(response.Name); // "User 1"
```

## Features

- Asynchronous request/response handling with optional buffering
- Cache-backed async processing pipeline
- Notification pub/sub with typed subscribers
- **Notification and request delivery with buffering control**
  - **Buffered mode (`enableBuffering: true`)**: Messages pass through an ordered cache before delivery. Useful when handlers are required to process events in the order of occurrence and/or the system requires durability/rewind-replay (look at [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB) for persistence)
  - **Unbuffered mode (`enableBuffering: false`)**: Messages delivered directly to handlers for low-latency processing
- **Cooperative cancellation support**
  - `CancellationToken` passed through all publish and subscribe operations
  - Subscribers and handlers receive cancellation tokens from their subscription for graceful shutdown
  - Handlers (`IRequestHandler`, `IAsyncRequestHandler`) and subscribers (`ISubscriber`) receive subscription's cancellation token
  - Early return from `Publish` when cancellation is requested
- Handler registration with cancellation token lifecycle
- Thread-safe concurrent access
- Function-based handler subscriptions

## API Reference

### IMediator

| Method | Description |
|--------|-------------|
| `Publish<T>(notification, cancellationToken)` | Publish a notification synchronously to subscribers. Checks cancellation before delivering to each subscriber. |
| `PublishAsync<T>(notification, cancellationToken)` | Publish a notification asynchronously (fire and forget). Passes cancellation token to Publish. |
| `PublishAsync<TRequest, TResponse>(request, cancellationToken)` | Publish a request and await response from registered handler. Cancellation token is monitored during processing. |
| `SubscribeAsync<T>(subscriber, enableBuffering, cancellationToken)` | Subscribe to notifications with `ISubscriber<T>`. Cancellation token ends subscription. |
| `SubscribeAsync<T>(subscriber, enableBuffering, name, cancellationToken)` | Subscribe to notifications with named cache enumerator. |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, cancellationToken)` | Register request handler with `IRequestHandler<TRequest, TResponse>`. |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, name, cancellationToken)` | Register request handler with named cache enumerator. |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, cancellationToken)` | Register async request handler with `IAsyncRequestHandler<TRequest, TResponse>`. |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, name, cancellationToken)` | Register async request handler with named cache enumerator. |
| `SubscribeAsync<TNotification>(func, enableBuffering, cancellationToken)` | Subscribe to notifications using function handler. Function receives cancellation token. |
| `SubscribeAsync<TNotification>(func, enableBuffering, name, cancellationToken)` | Subscribe to notifications using function handler with named enumerator. |
| `SubscribeAsync<TRequest, TResponse>(func, enableBuffering, cancellationToken)` | Register async request handler using function. Function receives cancellation token. |
| `SubscribeAsync<TRequest, TResponse>(func, enableBuffering, name, cancellationToken)` | Register async request handler using function with named enumerator. |

### Handler Interfaces

- `IRequestHandler<TRequest, TResponse>` - Synchronous handler with `Handle(TRequest, CancellationToken)` method. Receives subscription's cancellation token.
- `IAsyncRequestHandler<TRequest, TResponse>` - Asynchronous handler with `HandleAsync(TRequest, CancellationToken)` method. Receives subscription's cancellation token.
- `ISubscriber<T>` - Notification subscriber with `OnNext(T, CancellationToken)` method. Receives subscription's cancellation token.

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
    public bool OnNext(OrderCreated notification, CancellationToken cancellationToken = default)
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
var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var configuration = new Baubit.Caching.Configuration();
Func<long?, long?> nextIdFactory = (lastId) => Interlocked.Increment(ref lastId ?? 0);
var store = new Baubit.Caching.InMemory.Store<long, object>(null, null, nextIdFactory, loggerFactory);
var metadata = new Baubit.Caching.InMemory.Metadata<long>(configuration, loggerFactory);
var cache = new Baubit.Caching.OrderedCache<long, object>(configuration, null, store, metadata, loggerFactory);
var mediator = new Mediator(cache, loggerFactory);
using var cts = new CancellationTokenSource();

// Subscribe with buffering enabled (default)
var subscriber = new OrderNotificationSubscriber();
var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: true, cts.Token);

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
var subscribeTask = mediator.SubscribeAsync(subscriber, enableBuffering: false, cts.Token);

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
var bufferedTask = mediator.SubscribeAsync(bufferedSubscriber, enableBuffering: true, cts.Token);
var directTask = mediator.SubscribeAsync(directSubscriber, enableBuffering: false, cts.Token);

// Publish once - buffered subscriber gets it from cache, direct subscriber gets immediate delivery
mediator.Publish(new OrderCreated { OrderId = 3, Amount = 199.99m });

// Both subscribers receive the notification via their preferred delivery mechanism
```

### Cancellation Token Support

The mediator supports cooperative cancellation throughout the notification and request/response pipeline.

#### Cancelling Notification Delivery

```csharp
using var cts = new CancellationTokenSource();

// Subscribe with function handler that respects cancellation
var subscribeTask = mediator.SubscribeAsync<OrderCreated>(
    async (notification, ct) =>
    {
        // Handler receives cancellation token from Publish call
        if (ct.IsCancellationRequested) return true;
        
        await ProcessOrderAsync(notification, ct);
        return true;
    },
    enableBuffering: false,
    cts.Token
);

// Publish with cancellation token
var publishCts = new CancellationTokenSource();
mediator.Publish(new OrderCreated { OrderId = 1, Amount = 99.99m }, publishCts.Token);

// If cancellation is requested during Publish, delivery stops early
publishCts.Cancel();
mediator.Publish(new OrderCreated { OrderId = 2, Amount = 149.99m }, publishCts.Token);
// Returns immediately without delivering to subscribers
```

#### Subscriber OnNext with Cancellation

```csharp
public class OrderSubscriber : ISubscriber<OrderCreated>
{
    public bool OnNext(OrderCreated notification, CancellationToken cancellationToken = default)
    {
        // Check cancellation before processing
        if (cancellationToken.IsCancellationRequested)
            return true;
        
        // Perform work that respects cancellation
        ProcessOrder(notification, cancellationToken);
        return true;
    }
    
    public bool OnError(Exception error) => true;
    public bool OnCompleted() => true;
    public void Dispose() { }
}
```

#### Handlers with Cancellation

Request handlers also receive the subscription's cancellation token:

```csharp
public class GetUserHandler : IAsyncRequestHandler<GetUserRequest, GetUserResponse>
{
    public async Task<GetUserResponse> HandleAsync(GetUserRequest request, CancellationToken cancellationToken = default)
    {
        // The cancellation token comes from the subscription, not the Publish call
        // This allows the handler to gracefully shut down when subscription is cancelled
        if (cancellationToken.IsCancellationRequested)
            return new GetUserResponse { Name = "Cancelled" };
        
        var user = await database.GetUserAsync(request.UserId, cancellationToken);
        return new GetUserResponse { Name = user.Name };
    }
}

// Register handler with cancellation token
using var cts = new CancellationTokenSource();
var subscribeTask = mediator.SubscribeAsync<GetUserRequest, GetUserResponse>(
    new GetUserHandler(), 
    enableBuffering: false, 
    cts.Token  // Handler will receive this token
);

// When you cancel, handler gets notified
cts.Cancel();
```

### Request/Response Mediation

```csharp
// Publish request asynchronously and await response
var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 1 }
);

// All request handling is asynchronous
// Buffered mode (enableBuffering: true) tracks request/response through cache
// Unbuffered mode (enableBuffering: false) delivers directly to handler
```

### Function-Based Subscriptions

For scenarios where creating a full handler class is unnecessary, use function-based subscriptions:

#### Notification Handler Functions

```csharp
// Subscribe to notifications using a function handler
using var cts = new CancellationTokenSource();
var subscribeTask = mediator.SubscribeAsync<OrderCreated>(
    async (notification, ct) =>
    {
        Console.WriteLine($"Order {notification.OrderId} received");
        await ProcessOrderAsync(notification, ct);
        return true;
    },
    enableBuffering: true,
    cts.Token
);

// Publish notifications
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
    cts.Token
);

// Publish async request - function handler processes it
var response = await mediator.PublishAsync<GetUserRequest, GetUserResponse>(
    new GetUserRequest { UserId = 1 },
    CancellationToken.None
);

// Cancel subscription when done
cts.Cancel();
```

## Architecture Notes

**MediatR vs Baubit.Mediation**:
- **MediatR**: Offers built-in pipeline behaviors optimized for in-memory processing
- **Baubit.Mediation**: Expects pipelines to be built outside of its knowledge, focusing on cache-backed durability and distributed messaging

**Cache-Backed Async Mediation**:

Baubit.Mediation is powered by [Baubit.Caching](https://github.com/pnagoorkar/Baubit.Caching/), a high-performance hybrid cache. Baubit.Caching is being extended to support distributed systems - once complete, Baubit.Mediation will natively support distributed mediation scenarios.

## License


[MIT](LICENSE)



