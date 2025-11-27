using BenchmarkDotNet.Running;

namespace Baubit.Mediation.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<MediatorBenchmarks>();
    }
}

#region Baubit.Mediation Types

public class BaubitRequest : IRequest<BaubitResponse>
{
    public int Value { get; set; }
}

public class BaubitResponse : IResponse
{
    public int Result { get; set; }
}

public class BaubitNotification
{
    public string Message { get; set; } = string.Empty;
}

public class BaubitSyncHandler : IRequestHandler<BaubitRequest, BaubitResponse>
{
    public BaubitResponse Handle(BaubitRequest request)
    {
        return new BaubitResponse { Result = request.Value * 2 };
    }

    public Task<BaubitResponse> HandleSyncAsync(BaubitRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Handle(request));
    }

    public void Dispose() { }
}

public class BaubitNotificationSubscriber : ISubscriber<BaubitNotification>
{
    private int _count = 0;

    public bool OnNext(BaubitNotification next)
    {
        System.Threading.Interlocked.Increment(ref _count);
        return true;
    }

    public bool OnError(Exception error)
    {
        return true;
    }

    public bool OnCompleted()
    {
        return true;
    }

    public void Dispose() { }
}

#endregion

#region MediatR Types

public class MediatRRequest : MediatR.IRequest<MediatRResponse>
{
    public int Value { get; set; }
}

public class MediatRResponse
{
    public int Result { get; set; }
}

public class MediatRNotification : MediatR.INotification
{
    public string Message { get; set; } = string.Empty;
}

public class MediatRHandler : MediatR.IRequestHandler<MediatRRequest, MediatRResponse>
{
    public Task<MediatRResponse> Handle(MediatRRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new MediatRResponse { Result = request.Value * 2 });
    }
}

public class MediatRNotificationHandler : MediatR.INotificationHandler<MediatRNotification>
{
    private int _count = 0;

    public Task Handle(MediatRNotification notification, CancellationToken cancellationToken)
    {
        System.Threading.Interlocked.Increment(ref _count);
        return Task.CompletedTask;
    }
}

#endregion
