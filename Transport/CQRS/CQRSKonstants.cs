namespace Transport
{
    public static class CQRSKonstants
    {
        public const string K_EXCHANGE_TYPE_TOPIC = "topic";
        public const string KCommandQueue = "NQ.Commands";
        public const string KQueryQueue = "NQ.Queries";
        public const string KNQExchange = "NQ.Exchange";
        public const string KCommandTopic = "command";
        public const string KQueryTopic = "query";
    }
}