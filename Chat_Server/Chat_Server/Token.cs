using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;

namespace Chat_Server
{
    class Token
    {
        public Socket socket { get; set; }
        public SocketAsyncEventArgs receive_event_args { get; private set; }
        public SocketAsyncEventArgs send_event_args { get; private set; }

        public delegate void ClosedDelegate(Token token);
        public ClosedDelegate on_session_closed;

        public string client_ID;
        int is_closed;
        public Token(string client_ID)
        {
            this.client_ID = client_ID;
        }
        public void set_event_args(SocketAsyncEventArgs send_args, SocketAsyncEventArgs receive_args)
        {
            this.send_event_args = send_args;
            this.receive_event_args = receive_args;
        }
        
        
        public void token_close()
        {
            Console.WriteLine("token close");
            if(Interlocked.CompareExchange(ref this.is_closed, 1, 0) == 1)
            {
                return;
            }
            this.socket.Close();
            this.socket = null;
            this.send_event_args = null;
            this.receive_event_args = null;
        }
    }
}
