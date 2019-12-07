using LanguageExt;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using static LanguageExt.Prelude;


namespace Transport
{
    public class SerializerImpl : ISerializer
    {
        private readonly BinaryFormatter _bf;

        public SerializerImpl()
        {
            _bf = new BinaryFormatter();
        }

        public Option<byte[]> Serialize<TCommand>(TCommand command)
        {
            var ret = Option<byte[]>.None;

            
            using (var ms = new MemoryStream())
            {
                _bf.Serialize(ms, command);
                if(ms.TryGetBuffer(out var arrSeqment))
                    ret = Some(arrSeqment.ToArray());
            }

            //var serializedObj = JsonConvert.SerializeObject(command);
            //var data = Encoding.UTF8.GetBytes(serializedObj);

            return ret;
        }
        public Option<object> Deserialize(byte[] bytes)
        {
            var ret = Option<object>.None;

            using (var ms = new MemoryStream(bytes))
            {
                var obj = _bf.Deserialize(ms);
                ret = Some(obj);
            }
            return ret;
        }
    }
}
