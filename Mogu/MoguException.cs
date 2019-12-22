namespace Mogu
{
    [System.Serializable]
    public class MoguException : System.Exception
    {
        public MoguException() { }
        public MoguException(string message) : base(message) { }
        public MoguException(string message, System.Exception inner) : base(message, inner) { }
        protected MoguException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
