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
    public GetUserResponse Handle(GetUserRequest request, CancellationToken cancellationToken = default)
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
- **Built-in middleware pipeline for notification processing**
  - Compose reusable `Segment` middleware using `PipelineBuilder<T>`
  - Segments execute in declaration order; any segment may short-circuit the chain
- **Notification and request delivery with buffering control**
  - **Buffered mode (`enableBuffering: true`)**: Messages pass through an ordered cache before delivery. Useful when handlers are required to process events in the order of occurrence and/or the system requires durability/rewind-replay (look at [Baubit.Caching.LiteDB](https://github.com/pnagoorkar/Baubit.Caching.LiteDB) for persistence)
  - **Unbuffered mode (`enableBuffering: false`)**: Messages delivered directly to handlers for low-latency processing
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
| `PublishAsync<TRequest, TSegment, TResponse>(request, cancellationToken)` | Publish a stream request and iterate segments from the registered handler. Returns `IAsyncEnumerable<TSegment>`. |
| `SubscribeAsync<T>(subscriber, enableBuffering, cancellationToken)` | Subscribe to notifications with `ISubscriber<T>`. Cancellation token ends subscription. |
| `SubscribeAsync<T>(subscriber, enableBuffering, name, cancellationToken)` | Subscribe to notifications with named cache enumerator. |
| `SubscribeAsync<T>(pipelineBuildAction, enableBuffering, cancellationToken)` | Subscribe to notifications through a `PipelineBuilder<T>`-configured middleware pipeline. |
| `SubscribeAsync<T>(pipelineBuildAction, enableBuffering, name, cancellationToken)` | Subscribe to notifications through a `PipelineBuilder<T>`-configured middleware pipeline with a named cache enumerator. |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, cancellationToken)` | Register request handler with `IRequestHandler<TRequest, TResponse>`. |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, name, cancellationToken)` | Register request handler with named cache enumerator. |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, cancellationToken)` | Register async request handler with `IAsyncRequestHandler<TRequest, TResponse>`. |
| `SubscribeAsync<TRequest, TResponse>(handler, enableBuffering, name, cancellationToken)` | Register async request handler with named cache enumerator. |
| `SubscribeAsync<TNotification>(func, enableBuffering, cancellationToken)` | Subscribe to notifications using function handler. Function receives cancellation token. |
| `SubscribeAsync<TNotification>(func, enableBuffering, name, cancellationToken)` | Subscribe to notifications using function handler with named enumerator. |
| `SubscribeAsync<TRequest, TResponse>(func, enableBuffering, cancellationToken)` | Register async request handler using function. Function receives cancellation token. |
| `SubscribeAsync<TRequest, TResponse>(func, enableBuffering, name, cancellationToken)` | Register async request handler using function with named enumerator. |
| `SubscribeAsync<TRequest, TSegment, TResponse>(handler, enableBuffering, cancellationToken)` | Register stream request handler with `IAsyncStreamRequestHandler<TRequest, TSegment, TResponse>`. |
| `SubscribeAsync<TRequest, TSegment, TResponse>(handler, enableBuffering, name, cancellationToken)` | Register stream request handler with named cache enumerator. |
| `SubscribeAsync<TRequest, TSegment, TResponse>(func, enableBuffering, cancellationToken)` | Register stream request handler using function `Func<TRequest, CancellationToken, IAsyncEnumerable<TSegment>>`. |
| `SubscribeAsync<TRequest, TSegment, TResponse>(func, enableBuffering, name, cancellationToken)` | Register stream request handler using function with named enumerator. |

### Handler Interfaces

- `IRequestHandler<TRequest, TResponse>` - Synchronous handler with `Handle(TRequest, CancellationToken)` method. Receives subscription's cancellation token.
- `IAsyncRequestHandler<TRequest, TResponse>` - Asynchronous handler with `HandleAsync(TRequest, CancellationToken)` method. Receives subscription's cancellation token.
- `IAsyncStreamRequestHandler<TRequest, TSegment, TResponse>` - Asynchronous stream handler with `HandleAsync(TRequest, CancellationToken)` returning `IAsyncEnumerable<TSegment>`.
- `ISubscriber<T>` - Notification subscriber with `OnNext(T, CancellationToken)` method. Receives subscription's cancellation token.

### Stream Request Types

- `IStreamRequest<TSegment, TResponse>` - Marker interface for stream requests that produce sequences of `TSegment` values.
- `ISegment<TResponse>` - Marker interface for a single segment in a streamed response sequence.

### Pipeline API

`IPipeline<T>` and `PipelineBuilder<T>` enable composable middleware processing for notifications.

| Type | Description |
|------|-------------|
| `IPipeline<T>` | Contract for a middleware pipeline that processes items of type `T`. Exposes `RunAsync(T, CancellationToken)`. |
| `IPipeline<T>.Segment` | Delegate for a single middleware unit. Receives the item, a `Next` continuation for the remainder of the chain, and a cancellation token. |
| `PipelineBuilder<T>` | Fluent builder for composing `Segment` delegates into an `IPipeline<T>`. |
| `PipelineBuilder<T>.Use(segment)` | Registers a segment. Duplicate delegate references are ignored. |
| `PipelineBuilderExtensions.Use(segment)` | Extension for result-wrapped builders — chains `Use` in a fluent, railway-oriented style. |
| `PipelineBuilderExtensions.WithBuildAction(action)` | Applies an imperative configuration action to the wrapped builder. |
| `PipelineBuilderExtensions.Build()` | Finalises the builder and returns the constructed `IPipeline<T>`. |

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

### Stream Request/Response Mediation

Stream requests produce a sequence of segments (`IAsyncEnumerable<TSegment>`) rather than a single response.
Use this when a handler needs to push back multiple values over time.

```csharp
// Types
public class ChatRequest : IStreamRequest<TextChunk, ChatResponse> { public string Prompt { get; set; } }
public class TextChunk   : ISegment<ChatResponse>                  { public string Text   { get; set; } }
public class ChatResponse : IResponse { }

public class SpeechRequest : IStreamRequest<AudioChunk, AudioResponse> { public string Text { get; set; } }
public class AudioChunk   : ISegment<AudioResponse>                   { public byte[] Pcm  { get; set; } }
public class AudioResponse : IResponse { }

// Handlers
public class ChatHandler : IAsyncStreamRequestHandler<ChatRequest, TextChunk, ChatResponse>
{
    public async IAsyncEnumerable<TextChunk> HandleAsync(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in chatClient.CompleteChatStreamingAsync(request.Prompt, cancellationToken))
            foreach (var part in update.ContentUpdate)
                if (!string.IsNullOrEmpty(part.Text))
                    yield return new TextChunk { Text = part.Text };
    }
}

public class SpeechHandler : IAsyncStreamRequestHandler<SpeechRequest, AudioChunk, AudioResponse>
{
    public async IAsyncEnumerable<AudioChunk> HandleAsync(SpeechRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var buffer in ttsClient.SynthesisStreamingAsync(request.Text, cancellationToken))
            yield return new AudioChunk { Pcm = buffer };
    }
}

// Usage — each text chunk is forwarded as a new request to the TTS handler
using var cts = new CancellationTokenSource();
_ = mediator.SubscribeAsync<ChatRequest, TextChunk, ChatResponse>(new ChatHandler(), enableBuffering: true, cts.Token);
_ = mediator.SubscribeAsync<SpeechRequest, AudioChunk, AudioResponse>(new SpeechHandler(), enableBuffering: true, cts.Token);

await foreach (var textChunk in mediator.PublishAsync<ChatRequest, TextChunk, ChatResponse>(
    new ChatRequest { Prompt = "..." }, cts.Token))
{
    await foreach (var audioChunk in mediator.PublishAsync<SpeechRequest, AudioChunk, AudioResponse>(
        new SpeechRequest { Text = textChunk.Text }, cts.Token))
    {
        await audioPlayer.PlayAsync(audioChunk.Pcm, cts.Token);
    }
}

cts.Cancel();
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

### Middleware Pipeline for Notifications

`PipelineBuilder<T>` lets you compose reusable middleware segments that are chained together
and run in declaration order for each incoming notification.  Use it to add cross-cutting
concerns such as logging, validation, or filtering without modifying your core handler logic.

```csharp
// Subscribe using a middleware pipeline
using var cts = new CancellationTokenSource();

var subscribeTask = mediator.SubscribeAsync<OrderCreated>(
    pb =>
    {
        // Segment 1 — logging
        pb.Use(async (order, next, ct) =>
        {
            Console.WriteLine($"[LOG] Received order {order.OrderId}");
            return await next(order, ct); // call next to continue the chain
        });

        // Segment 2 — validation / filtering
        pb.Use(async (order, next, ct) =>
        {
            if (order.Amount <= 0)
            {
                Console.WriteLine($"[SKIP] Order {order.OrderId} has non-positive amount — dropping");
                return false; // short-circuit: segment 3 will not run
            }
            return await next(order, ct);
        });

        // Segment 3 — actual processing
        pb.Use(async (order, next, ct) =>
        {
            await ProcessOrderAsync(order, ct);
            return await next(order, ct);
        });
    },
    enableBuffering: true,
    cts.Token
);

mediator.Publish(new OrderCreated { OrderId = 1, Amount = 99.99m });
mediator.Publish(new OrderCreated { OrderId = 2, Amount = -5m }); // filtered out by segment 2

cts.Cancel();
```

**Key behaviours:**
- Segments run in the order they are registered via `Use`.
- Calling `next` passes control to the following segment; the implicit terminal segment at the end of every chain returns `true`.
- Omitting the `next` call short-circuits the chain and the return value of the current segment is used as the pipeline result.
- Registering the same delegate reference more than once is a no-op (the second registration is silently ignored).
- Use the named overload (`name` parameter) when multiple independent pipeline subscriptions for the same notification type each need their own cache enumerator position:

```csharp
// Two independent pipelines with separate named enumerators
var sub1 = mediator.SubscribeAsync<OrderCreated>(
    pb => pb.Use(async (order, next, ct) => { /* audit log */ return await next(order, ct); }),
    enableBuffering: true,
    name: "audit-pipeline",
    cts.Token);

var sub2 = mediator.SubscribeAsync<OrderCreated>(
    pb => pb.Use(async (order, next, ct) => { /* billing */ return await next(order, ct); }),
    enableBuffering: true,
    name: "billing-pipeline",
    cts.Token);

mediator.Publish(new OrderCreated { OrderId = 1, Amount = 99.99m });
// Both pipelines receive the notification independently via their own named cache positions
```

## Architecture Notes


**Cache-Backed Async Mediation**:

Baubit.Mediation is powered by [Baubit.Caching](https://github.com/pnagoorkar/Baubit.Caching/), a high-performance hybrid cache. Baubit.Caching is being extended to support distributed systems - once complete, Baubit.Mediation will natively support distributed mediation scenarios.

## License


[MIT](LICENSE)


