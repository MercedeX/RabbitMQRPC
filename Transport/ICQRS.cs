using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace Transport
{
    public interface ICQRS
    {
        Task<Unit> Execute<TCommand>(TCommand command, CancellationToken token);
        Task<object> Ask<TRequest>(TRequest request, CancellationToken token);
    }
}
