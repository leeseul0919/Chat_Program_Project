using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;

namespace Chat_Client
{
    class Program
    {
        static public List<Token> serverlist;
        static void Main(string[] args)
        {

            int port = 7979;
            string serverIP = "192.168.0.175";
            serverlist = new List<Token>();

            IPAddress address = IPAddress.Parse(serverIP);
            IPEndPoint endpoint = new IPEndPoint(address, port);
            Connector connector = new Connector();
            connector.connected_callback += on_connected_gameserver;
            connector.connect(endpoint);
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
            }
            connector.connector_close();
            Console.ReadKey();
        }
        static void on_connected_gameserver(Token server_token)
        {
            lock (serverlist)
            {
                serverlist.Add(server_token);
            }
        }
    }
}
