using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace Transport
{
    public interface ICQRS
    {
        Task<Unit> Execute<TCommand>(TCommand command, CancellationToken token);
        Task<TResponse> Ask<TResponse, TRequest>(TRequest request, CancellationToken token);
    }
}
