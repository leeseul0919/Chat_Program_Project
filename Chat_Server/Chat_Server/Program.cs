using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace Chat_Server
{
    class Program
    {
        static void Main(string[] args)
        {
            NetworkService server = new NetworkService();
            server.start("MY Server Computer IP ADDRESS", 7979, 100); //start listen
            while(true)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
