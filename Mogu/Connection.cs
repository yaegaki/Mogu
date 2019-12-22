using System;
using System.IO;
using System.IO.Pipes;

namespace Mogu
{
    public class Connection : IDisposable
    {
        public bool IsAutoClose { get; private set; }
        public bool IsConnected => Pipe.IsConnected;
        public PipeStream Pipe { get; }

        public Connection(NamedPipeServerStream pipe)
        {
            this.Pipe = pipe;
            this.IsAutoClose = false;
        }

        public Connection(NamedPipeClientStream pipe)
        {
            this.Pipe = pipe;
            this.IsAutoClose = true;
        }
        
        public void PreventAutoClose()
            => IsAutoClose = false;

        public void Dispose()
            => this.Pipe.Dispose();
    }
}
