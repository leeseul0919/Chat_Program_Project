using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;

namespace Chat_Server
{
    class ChatManager
    {
        Queue<chat_info_request> chat_manager_queue;
        NetworkService server_network;
        public RoomManager current_tokens_receive;
        public class chat_info_request
        {
            public Token client_token;
            public room_create_message room_message { get; set; }
        }
        public ChatManager()
        {
            chat_manager_queue = new Queue<chat_info_request>();
        }
        public void start_chatmanager(NetworkService server_network)
        {
            this.server_network = server_network;
            Console.WriteLine("start chatmanager");
            Thread room_manager_thread = new Thread(chat_info_dolisten);
            room_manager_thread.Start();
        }
        public void set_roomManager(RoomManager deliver_RoomManager)
        {
            this.current_tokens_receive = deliver_RoomManager;
            Console.WriteLine("set roommanager" + this.current_tokens_receive);
        }
        public void chat_info_dolisten()
        {
            while (true)
            {
                chat_info_request request;
                lock (chat_manager_queue)
                {
                    if (chat_manager_queue.Count == 0) continue;
                    request = chat_manager_queue.Dequeue();
                }
                int send_room_num = request.room_message.room_num;
                List<Token> send_client_tokens = current_tokens_receive.deliver_current_tokens(send_room_num, request.room_message.send_message);

                room_create_message new_message = new room_create_message();
                new_message.pt_id = PROTOCOL.Deliver_Message;
                new_message.room_num = request.room_message.room_num;
                new_message.send_message = request.room_message.send_message;
                string new_deliver_message = JsonConvert.SerializeObject(new_message);
                byte[] messageBuffer = Encoding.UTF8.GetBytes(new_deliver_message);
                foreach (var current_token in send_client_tokens)
                {
                    Console.WriteLine("delive message");
                    current_token.socket.Send(messageBuffer);
                }
            }
        }
        public void enqueue_chat_message(Token client_token, room_create_message received_chat_message)
        {
            Console.WriteLine("chat queue add");
            chat_info_request request_message = new chat_info_request();
            request_message.client_token = client_token;
            request_message.room_message = received_chat_message;
            chat_manager_queue.Enqueue(request_message);
        }
    }
}
