using System;

namespace Transport
{
    public class CommandArgs : EventArgs
    {
        public object Command { get;  }
        public Guid Key { get;   }

        public CommandArgs(Guid key, object command)
        {
            Key = key;
            Command = command;
        }
    }
}