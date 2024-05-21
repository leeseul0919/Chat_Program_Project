using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using Newtonsoft.Json;
using MongoDB.Driver;
using MongoDB.Bson;

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
        ChatManager chat_manager;

        public IMongoCollection<BsonDocument> ChatRoomsCollection;
        private const string Rooms_Collection = "ChatRooms";

        public RoomManager()
        {
            Chat_Rooms = new List<Room>();
            client_roominfo_message = new Queue<room_info_request>();
        }
        public List<Room> Find_room_list(string ID)
        {
            List<Room> room_list = new List<Room>();
            var allDocuments = ChatRoomsCollection.Find(new BsonDocument()).ToList();
            foreach(var doc in allDocuments)
            {
                var invitedIdsArray = doc["Invited_IDs"] as BsonArray;
                List<string> invitedIdsList = invitedIdsArray.Select(bsonValue => bsonValue.AsString).ToList();
                foreach(var room_ID in invitedIdsList)
                {
                    if (room_ID.Equals(ID))
                    {
                        Room new_Room = new Room(doc["Room_Num"].AsInt32, doc["Room_Name"].AsString, invitedIdsList);
                        room_list.Add(new_Room);
                        break;
                    }
                }
            }
            return room_list;
        }
        public void start_roommanager(NetworkService server_network)
        {
            this.server_network = server_network;
            ChatRoomsCollection = server_network.database.GetCollection<BsonDocument>(Rooms_Collection);
            Console.WriteLine("start roommanager");
            Thread room_manager_thread = new Thread(room_info_dolisten);
            room_manager_thread.Start();
        }
        public void set_chatManager(ChatManager deliver_chatManager)
        {
            this.chat_manager = deliver_chatManager;
            Console.WriteLine("set roommanager" + this.chat_manager);
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
                    var allDocuments = ChatRoomsCollection.Find(new BsonDocument()).ToList();
                    int maxRoomNum = 0;
                    foreach (var document in allDocuments)
                    {
                        if(document["Room_Num"].AsInt32 > maxRoomNum)
                        {
                            maxRoomNum = document["Room_Num"].AsInt32;
                        }
                    }
                    int newRoomNum = maxRoomNum + 1;

                    room_create_message send_room_ready_request = new room_create_message();
                    send_room_ready_request.invite_IDs = new List<string>();
                    send_room_ready_request.pt_id = PROTOCOL.Room_Create_Success;
                    send_room_ready_request.invite_IDs = request.room_message.invite_IDs;
                    List<Token> deliver_update_to_tokens = server_network.room_update_send(request.room_message.invite_IDs);
                    send_room_ready_request.invite_IDs.Add(request.client_token.client_ID);
                    send_room_ready_request.room_num = newRoomNum;
                    string jsonData = JsonConvert.SerializeObject(send_room_ready_request);
                    byte[] send_buffer = Encoding.UTF8.GetBytes(jsonData);
                    request.client_token.socket.Send(send_buffer);

                    var newRoomDoc = new BsonDocument { { "Room_Num", newRoomNum }, { "Room_Name", request.room_message.room_name }, { "Invited_IDs", new BsonArray(request.room_message.invite_IDs) } };
                    ChatRoomsCollection.InsertOne(newRoomDoc);

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
                    bool isroom = false;
                    List<string> deliver_room_messages = new List<string>();
                    room_create_message send_room_enter_request = new room_create_message();
                    send_room_enter_request.pt_id = PROTOCOL.Room_Enter_Success;

                    foreach (var room_info in Chat_Rooms)
                    {
                        if (room_info.room_num == request.room_message.room_num)
                        {
                            deliver_room_messages = room_info.room_messages;
                            send_room_enter_request.room_num = room_info.room_num;
                            send_room_enter_request.room_messages = deliver_room_messages;
                            room_info.current_contact_users.Add(request.client_token);
                            isroom = true;
                            break;
                        }
                    }

                    if(!isroom)
                    {
                        var filter = new BsonDocument();
                        var docs = ChatRoomsCollection.Find(filter).ToList();
                        foreach (var doc in docs)
                        {
                            if (request.room_message.room_num == doc["Room_Num"].AsInt32)
                            {
                                var bsonArray = doc["Invited_IDs"].AsBsonArray;
                                List<string> inviteIds = new List<string>();
                                foreach (var bsonValue in bsonArray) inviteIds.Add(bsonValue.AsString);

                                Room new_room = new Room(doc["Room_Num"].AsInt32, doc["Room_Name"].AsString, inviteIds);
                                new_room.current_contact_users.Add(request.client_token);
                                Chat_Rooms.Add(new_room);

                                send_room_enter_request.room_num = doc["Room_Num"].AsInt32;
                                deliver_room_messages = chat_manager.db_messages_deliver(doc["Room_Num"].AsInt32);
                                send_room_enter_request.room_messages = deliver_room_messages;
                                break;
                            }
                        }
                    }
                    string jsonData = JsonConvert.SerializeObject(send_room_enter_request);
                    byte[] send_buffer = Encoding.UTF8.GetBytes(jsonData);
                    request.client_token.socket.Send(send_buffer);
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
                                    if (room_info.current_contact_users.Count == 0) Chat_Rooms.Remove(room_info);
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
