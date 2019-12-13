namespace Transport
{
    public class AMQPConfiguration
    {
        public string Host { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; }
        public int Port { get; set; }
        public bool SSL { get; set; }
    }
}