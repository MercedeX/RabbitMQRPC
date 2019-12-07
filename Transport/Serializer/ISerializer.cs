using LanguageExt;

namespace Transport
{
    public interface ISerializer
    {
        Option<byte[]> Serialize<TCommand>(TCommand command);
        Option<object> Deserialize(byte[] bytes);
    }
}