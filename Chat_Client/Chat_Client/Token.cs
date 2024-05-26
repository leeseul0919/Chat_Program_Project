using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;

namespace Chat_Client
{
    class Token
    {
        class room_create_info
        {
            public PROTOCOL pt_id { get; set; }
            public string ID { get; set; }
            public int room_num { get; set; }
            public string room_name { get; set; }
            public List<string> invite_IDs = new List<string>();
            public List<string> room_messages = new List<string>();
            public List<int> room_nums = new List<int>();
            public List<string> room_names = new List<string>();
            public string send_message { get; set; }
        }

        public Socket socket { get; set; }
        public SocketAsyncEventArgs receive_event_args { get; private set; }
        public SocketAsyncEventArgs send_event_args { get; private set; }

        public delegate void ClosedDelegate(Token token);
        public ClosedDelegate on_session_closed;

        public string client_ID;
        int is_closed;
        public List<Room> client_room_list;

        Queue<room_create_info> message_queue;
        public Token(string client_ID)
        {
            this.client_ID = client_ID;
            this.client_room_list = new List<Room>();
            this.message_queue = new Queue<room_create_info>();
        }
        public void set_event_args()
        {
            this.receive_event_args = new SocketAsyncEventArgs();
            receive_event_args.UserToken = socket;
            byte[] buffer = new byte[1024];
            receive_event_args.SetBuffer(buffer, 0, buffer.Length);
            receive_event_args.Completed += Message_received_completed;
        }
        public void set_room_list(login_success_info server_send_rooms)
        {
            for(int i=0;i<server_send_rooms.room_nums.Count;i++)
            {
                Room client_room = new Room(server_send_rooms.room_nums[i], server_send_rooms.room_names[i]);
                client_room_list.Add(client_room);
            }
            Console.WriteLine("---------------------------------------");
        }
        public void start_chat()
        {
            Console.WriteLine("Chat program Start");

            start_receive_thread();
            chat_lobby();
            process_message_queue();
        }
        void start_receive_thread()
        {
            Thread receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        public void process_message_queue()
        {
            while (true)
            {
                room_create_info request;
                lock (message_queue)
                {
                    if (message_queue.Count == 0) continue;
                    request = message_queue.Dequeue();
                }
                if (request.pt_id == PROTOCOL.Room_Create_Ready_Success) set_roomname_invite(request.invite_IDs);
                else if (request.pt_id == PROTOCOL.Room_Create_Ready_Fail)
                {
                    Console.WriteLine("Room Create Ready Fail. Please try again");
                    chat_lobby();
                }
                else if (request.pt_id == PROTOCOL.Room_Create_Success)
                {
                    Room new_client_room = new Room(request.room_num, room_name);
                    client_room_list.Add(new_client_room);
                    Console.WriteLine("Room Create Success");
                    chat_lobby();
                }
                else if (request.pt_id == PROTOCOL.Room_Create_Fail)
                {
                    Console.WriteLine("Room Create Fail");
                    chat_lobby();
                }
                else if (request.pt_id == PROTOCOL.Room_Enter_Success)
                {
                    Console.WriteLine("Room Enter Success");
                    foreach (var room_info in client_room_list)
                    {
                        if (room_info.room_num == request.room_num)
                        {
                            room_info.room_messages = request.room_messages;
                            break;
                        }
                    }
                    chat_room(request.room_num);
                }
                else if (request.pt_id == PROTOCOL.Room_Enter_Fail)
                {
                    Console.WriteLine("Room Enter Fail");
                    chat_lobby();
                }
                else if (request.pt_id == PROTOCOL.Room_Leave_Success)
                {
                    Console.WriteLine("Room Leave Success");
                    chat_lobby();
                }
                else continue;
            }
        }
        void ReceiveData()
        {
            // 비동기 수신 시작
            bool pending = socket.ReceiveAsync(receive_event_args);
            if (!pending)
            {
                //Console.WriteLine("Delivered message");
                // 동기적으로 완료됨, 직접 완료 처리
                Message_received_completed(socket, receive_event_args);
            }
        }
        void Message_received_completed(object send, SocketAsyncEventArgs e)
        {
            try
            {
                int bytesReceived = e.BytesTransferred;
                if (bytesReceived > 0 && e.SocketError == SocketError.Success)
                {
                    byte[] receive_buffer = e.Buffer;
                    string server_send_message = Encoding.UTF8.GetString(receive_buffer, 0, bytesReceived);

                    room_create_info received_message = JsonConvert.DeserializeObject<room_create_info>(server_send_message);
                    if (received_message.pt_id == PROTOCOL.Deliver_Message) Console.WriteLine(received_message.ID + " >> " + received_message.send_message);
                    else if(received_message.pt_id == PROTOCOL.Room_Update)
                    {
                        Room update_room = new Room(received_message.room_num, received_message.room_name);
                        client_room_list.Add(update_room);
                        if(islobby) show_rooms();
                    }
                    else message_queue.Enqueue(received_message);
                    

                    bool pending = socket.ReceiveAsync(e);
                    if (!pending) Message_received_completed(send, e);
                }
                else
                {
                    on_session_closed(this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error handling receive: " + ex.Message);
            }
        }
        public bool isRoomnum(int select)
        {
            bool is_room = false;
            foreach (var room_info in client_room_list)
            {
                if (room_info.room_num == select)
                {
                    is_room = true;
                    break;
                }
            }
            return is_room;
        }
        void show_rooms()
        {
            Console.WriteLine("\n" + client_ID + "s Room List");
            foreach (var room_info in client_room_list) Console.WriteLine("[" + room_info.room_num + "] " + room_info.room_name);
            Console.WriteLine("chatting room create: 0 press >> ");
        }
        public bool islobby = false;
        void chat_lobby()
        {
            islobby = true;
            int enter_room_num = 0;
            PROTOCOL send_pt_id = PROTOCOL.Settings;
            Console.WriteLine(client_ID + "s Room List");
            foreach (var room_info in client_room_list) Console.WriteLine("[" + room_info.room_num + "] " + room_info.room_name);
            while (true)
            {
                Console.WriteLine("chatting room create: 0 press >> ");
                int select = Console.Read() - '0';
                if (select == 0)
                {
                    send_pt_id = PROTOCOL.Room_Create_Ready_Request;
                    break;
                }
                else if (isRoomnum(select))
                {
                    send_pt_id = PROTOCOL.Room_Enter_Request;
                    enter_room_num = select;
                    break;
                }
                Console.ReadLine();
            }
            islobby = false;
            if (send_pt_id == PROTOCOL.Room_Create_Ready_Request) create_room(send_pt_id);
            else if (send_pt_id == PROTOCOL.Room_Enter_Request) enter_room(send_pt_id, enter_room_num);
        }
        string room_name;
        void create_room(PROTOCOL pt_id)
        {
            Console.WriteLine("create room");
            room_create_info room_create_pt_id = new room_create_info();
            room_create_pt_id.pt_id = pt_id;
            string sendProtocol = JsonConvert.SerializeObject(room_create_pt_id);
            byte[] send_buffer = Encoding.UTF8.GetBytes(sendProtocol);
            socket.Send(send_buffer);
        }
        void enter_room(PROTOCOL pt_id, int room_num)
        {
            room_create_info room_enter_message = new room_create_info();
            room_enter_message.pt_id = pt_id;
            room_enter_message.room_num = room_num;
            string send_message = JsonConvert.SerializeObject(room_enter_message);
            byte[] send_buffer = Encoding.UTF8.GetBytes(send_message);
            socket.Send(send_buffer);
        }
        
        void chat_room(int room_num)
        {
            foreach (var room_info in client_room_list)
            {
                if (room_info.room_num == room_num)
                {
                    room_info.print_messages();
                    break;
                }
            }

            SendLoop(room_num);
        }
        void SendLoop(int room_num)
        {
            Console.ReadLine();
            while (true)
            {
                string message = Console.ReadLine();
                if (message.ToLower() == "q")
                {
                    break;
                }

                room_create_info new_send_message = new room_create_info();
                new_send_message.pt_id = PROTOCOL.Send_Message;
                new_send_message.ID = client_ID;
                new_send_message.send_message = message;
                new_send_message.room_num = room_num;
                string send_message = JsonConvert.SerializeObject(new_send_message);
                byte[] send_buffer = Encoding.UTF8.GetBytes(send_message);
                socket.Send(send_buffer);
            }
            room_leave_send(room_num);
        }
        void room_leave_send(int room_num)
        {
            room_create_info new_send_message = new room_create_info();
            new_send_message.pt_id = PROTOCOL.Room_Leave;
            new_send_message.room_num = room_num;
            string send_message = JsonConvert.SerializeObject(new_send_message);
            byte[] send_buffer = Encoding.UTF8.GetBytes(send_message);
            socket.Send(send_buffer);
        }
        
        void set_roomname_invite(List<string> all_IDs)
        {
            List<string> invite_IDs = new List<string>();
            Console.WriteLine("enter room name");
            Console.ReadLine();
            room_name = Console.ReadLine();
            foreach (var user in all_IDs) Console.WriteLine(">> " + user);
            Console.WriteLine("Select ID (Enter 0 to finish)");
            List<bool> isinvite = new List<bool>();
            for (int i = 0; i < all_IDs.Count; i++) isinvite.Add(false);
            while (true)
            {
                Console.Write(">> ");
                string input = Console.ReadLine().Trim();

                if (int.TryParse(input, out int all_IDs_index))
                {
                    if (all_IDs_index == 0) break;
                    else if ((all_IDs_index - 1) < all_IDs.Count && all_IDs_index > 0) 
                    {
                        isinvite[all_IDs_index - 1] = !isinvite[all_IDs_index - 1];
                        if(isinvite[all_IDs_index - 1]) Console.WriteLine($"Added ID: {all_IDs[all_IDs_index - 1]}");
                        else Console.WriteLine($"Cancel ID: {all_IDs[all_IDs_index - 1]}");
                    }
                    else Console.WriteLine("Invalid input, please try again.");
                }
                else
                {
                    Console.WriteLine("Invalid input, please enter a number.");
                }
            }
            for(int i=0;i<isinvite.Count;i++)
            {
                if (isinvite[i]) invite_IDs.Add(all_IDs[i]);
            }
            room_create_info room_create_message = new room_create_info();
            room_create_message.pt_id = PROTOCOL.Room_Create_Request;
            room_create_message.room_name = room_name;
            room_create_message.invite_IDs = invite_IDs;

            string send_room_create_message = JsonConvert.SerializeObject(room_create_message);
            byte[] send_message = Encoding.UTF8.GetBytes(send_room_create_message);
            socket.Send(send_message);
        }
        
        public void token_close()
        {
            Console.WriteLine("token close");
            if (Interlocked.CompareExchange(ref this.is_closed, 1, 0) == 1)
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
