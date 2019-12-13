using System;

namespace Transport
{
    public class QueryArgs : EventArgs
    {
        public object Query { get; }
        public Guid Key { get;   }

        public QueryArgs(Guid key, object query)
        {
            Key = key;
            Query = query;
        }

        public void SetResult<TObject>(TObject obj)
        {
            Result = obj;
        }

        public object Result { get; private set; }
    }
}