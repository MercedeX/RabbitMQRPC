using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace Transport
{
    public interface ICQRS
    {
        Task<Unit> Execute<TCommand>(TCommand command, CancellationToken token);
        Task<TResponse> Ask<TResponse>(object request, CancellationToken token)where TResponse: class;
    }
}
