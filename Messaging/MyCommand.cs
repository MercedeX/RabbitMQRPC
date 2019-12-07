using System;
using System.Runtime.Serialization;

namespace Messaging
{
    [Serializable]
    public class MyCommand: FluentValidation.AbstractValidator<MyCommand>, ISerializable
    {
        public int Port { get;}
        public string Name { get; }

        public MyCommand(string name, int port)
        {
            Name = name;
            Port = port;
        }


        public MyCommand(SerializationInfo info, StreamingContext context)
        {
            Port = info.GetInt32(nameof(Port));
            Name = info.GetString(nameof(Name));
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(Port), Port);
            info.AddValue(nameof(Name), Name);
        }
    }
}
