using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace Chat_Server
{
    class RoomManager
    {
        public class room_create_ready
        {
            public PROTOCOL pt_id { get; set; }
            public int room_num { get; set; }
            public List<string> IDs = new List<string>();
        }
        public class room_info_request
        {
            public Token client_token;
            public room_create_message room_message { get; set; }

        }
        List<Room> Chat_Rooms;

        AutoResetEvent flow_room_manager_event;

        NetworkService server_network;
        Queue<room_info_request> client_roominfo_message;
        public RoomManager()
        {
            Chat_Rooms = new List<Room>();
            client_roominfo_message = new Queue<room_info_request>();
        }
        public List<Room> Find_room_list(string ID)
        {
            List<Room> room_list = new List<Room>();
            foreach(var room_info in Chat_Rooms)
            {
                foreach(var room_ID in room_info.room_users)
                {
                    if(room_ID.Equals(ID))
                    {
                        room_list.Add(room_info);
                        break;
                    }
                }
            }
            return room_list;
        }
        public void start_roommanager(NetworkService server_network)
        {
            this.server_network = server_network;
            Console.WriteLine("start roommanager");
            Thread room_manager_thread = new Thread(room_info_dolisten);
            room_manager_thread.Start();
        }
        public void room_info_dolisten()
        {
            this.flow_room_manager_event = new AutoResetEvent(false);
            while (true)
            {
                room_info_request request;
                lock (client_roominfo_message)
                {
                    if (client_roominfo_message.Count == 0) continue;
                    request = client_roominfo_message.Dequeue();
                }
                if(request.room_message.pt_id == PROTOCOL.Room_Create_Ready_Request)
                {
                    room_create_message send_room_ready_request = new room_create_message();
                    send_room_ready_request.invite_IDs = new List<string>();
                    send_room_ready_request.pt_id = PROTOCOL.Room_Create_Ready_Success;
                    send_room_ready_request.invite_IDs = server_network.deliver_user_db(request.client_token);
                    send_room_ready_request.room_num = 0;
                    string jsonData = JsonConvert.SerializeObject(send_room_ready_request);
                    byte[] send_buffer = Encoding.UTF8.GetBytes(jsonData);
                    request.client_token.socket.Send(send_buffer);
                    Console.WriteLine(jsonData);

                    Console.WriteLine("send Room_Create_Ready_Success");
                }
                else if(request.room_message.pt_id == PROTOCOL.Room_Create_Request)
                {
                    Room new_room = new Room(Chat_Rooms.Count+1, request.room_message.room_name, request.room_message.invite_IDs);
                    Chat_Rooms.Add(new_room);

                    room_create_message send_room_ready_request = new room_create_message();
                    send_room_ready_request.invite_IDs = new List<string>();
                    send_room_ready_request.pt_id = PROTOCOL.Room_Create_Success;
                    send_room_ready_request.invite_IDs = request.room_message.invite_IDs;
                    List<Token> deliver_update_to_tokens = server_network.room_update_send(request.room_message.invite_IDs);
                    send_room_ready_request.invite_IDs.Add(request.client_token.client_ID);
                    send_room_ready_request.room_num = new_room.room_num;
                    string jsonData = JsonConvert.SerializeObject(send_room_ready_request);
                    byte[] send_buffer = Encoding.UTF8.GetBytes(jsonData);
                    request.client_token.socket.Send(send_buffer);

                    send_room_ready_request.pt_id = PROTOCOL.Room_Update;
                    send_room_ready_request.room_name = request.room_message.room_name;
                    jsonData = JsonConvert.SerializeObject(send_room_ready_request);
                    send_buffer = Encoding.UTF8.GetBytes(jsonData);
                    foreach (var current_token in deliver_update_to_tokens)
                    {
                        Console.WriteLine("deliver room update");
                        current_token.socket.Send(send_buffer);
                    }
                    Console.WriteLine("send Room_Create_Success");
                }
                else if(request.room_message.pt_id == PROTOCOL.Room_Enter_Request)
                {
                    List<string> deliver_room_messages = new List<string>();
                    foreach(var room_info in Chat_Rooms)
                    {
                        if(room_info.room_num == request.room_message.room_num)
                        {
                            room_info.current_contact_users.Add(request.client_token);
                            deliver_room_messages = room_info.room_messages;
                            room_create_message send_room_enter_request = new room_create_message();
                            send_room_enter_request.pt_id = PROTOCOL.Room_Enter_Success;
                            send_room_enter_request.room_num = request.room_message.room_num;
                            send_room_enter_request.room_messages = deliver_room_messages;
                            string jsonData = JsonConvert.SerializeObject(send_room_enter_request);
                            byte[] send_buffer = Encoding.UTF8.GetBytes(jsonData);
                            request.client_token.socket.Send(send_buffer);

                            break;
                        }
                    }
                    Console.WriteLine("send Room_Enter_Success");
                }
                else if(request.room_message.pt_id == PROTOCOL.Room_Leave)
                {
                    foreach (var room_info in Chat_Rooms)
                    {
                        if(room_info.room_num == request.room_message.room_num)
                        {
                            for (int i = room_info.current_contact_users.Count - 1; i >= 0; i--)
                            {
                                var token = room_info.current_contact_users[i];
                                if (token.client_ID == request.client_token.client_ID)
                                {
                                    Console.WriteLine("enter room true");
                                    room_info.current_contact_users.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                        break;
                    }
                    room_create_message send_room_leave_request = new room_create_message();
                    send_room_leave_request.pt_id = PROTOCOL.Room_Leave_Success;
                    string jsonData = JsonConvert.SerializeObject(send_room_leave_request);
                    byte[] send_buffer = Encoding.UTF8.GetBytes(jsonData);
                    request.client_token.socket.Send(send_buffer);
                    Console.WriteLine(jsonData);
                }
                
            }
        }
        public void enqueue_room_message(Token client_token, room_create_message received_room_message)
        {
            Console.WriteLine("room queue add");
            room_info_request request_message = new room_info_request();
            request_message.client_token = client_token;
            request_message.room_message = received_room_message;
            //request_message.room_message.invite_IDs.Add(client_token.client_ID);
            client_roominfo_message.Enqueue(request_message);
        }
        public List<Token> deliver_current_tokens(int room_num, string receive_message)
        {
            List<Token> give_tokens = new List<Token>();
            foreach (var room_info in Chat_Rooms)
            {
                if (room_info.room_num == room_num)
                {
                    room_info.room_messages.Add(receive_message);
                    give_tokens = room_info.current_contact_users;
                    break;
                }
            }
            return give_tokens;
        }
        public bool remove_current_token(Token client_token)
        {
            bool is_enter_room = false;
            foreach(var room_info in Chat_Rooms)
            {
                for (int i = room_info.current_contact_users.Count - 1; i >= 0; i--)
                {
                    var token = room_info.current_contact_users[i];
                    if (token.client_ID == client_token.client_ID)
                    {
                        is_enter_room = true;
                        Console.WriteLine("enter room true");
                        room_info.current_contact_users.RemoveAt(i);
                        break;
                    }
                }
            }
            return is_enter_room;
        }
    }
}
