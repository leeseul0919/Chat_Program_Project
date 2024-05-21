using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using Newtonsoft.Json;

namespace Chat_Server
{
    public class room_create_message
    {
        public PROTOCOL pt_id { get; set; }
        public int room_num { get; set; }
        public string room_name { get; set; }
        public List<string> invite_IDs = new List<string>();
        public List<string> room_messages = new List<string>();
        public List<int> room_nums = new List<int>();
        public List<string> room_names = new List<string>();
        public string send_message { get; set; }

    }
    class NetworkService
    {
        class login_info
        {
            public string ID { get; set; }
            public string PW { get; set; }
        }
        class accept_receive_data
        {
            public PROTOCOL pt_id { get; set; }
            public login_info info { get; set; }
        }
        class login_success_info
        {
            public PROTOCOL pt_id { get; set; }
            public List<int> room_nums = new List<int>();
            public List<string> room_names = new List<string>();
        }
        //listen 소켓
        Socket listen_socket;
        SocketAsyncEventArgs accept_event;
        AutoResetEvent flow_control_event;
        public delegate void ClientHandler(Socket client_socket, object token, string client_ID);
        public ClientHandler callback_on_newclient;

        public delegate void SessionHandler(Token token);
        public SessionHandler session_created_callback { get; set; }

        public Stack<SocketAsyncEventArgs> receive_event_pool;
        public Stack<SocketAsyncEventArgs> send_event_pool;

        //buffer 설정
        public byte[] m_buffer;
        public int max_connections;
        public int buffer_size;
        public int pre_alloc_count = 1;
        public int connected_count;

        public int m_numBytes;
        public int m_currentIndex = 0;
        public int m_bufferSize;
        public Stack<int> m_freeIndexPool;

        List<Socket> client_sockets;
        List<Token> users;
        List<login_info> fake_user_db;

        public RoomManager roomManager_cs;
        public ChatManager chatManager_cs;
        public NetworkService()
        {
            roomManager_cs = new RoomManager();
            chatManager_cs = new ChatManager();
            client_sockets = new List<Socket>();
            users = new List<Token>();

            fake_user_db = new List<login_info>();

            this.connected_count = 0;
            this.session_created_callback = null;

            this.max_connections = 10000;
            this.buffer_size = 1024;

            this.m_numBytes = this.max_connections * this.buffer_size * this.pre_alloc_count;
            this.m_bufferSize = this.buffer_size;
            this.m_buffer = new byte[m_numBytes];
            m_freeIndexPool = new Stack<int>();

            this.callback_on_newclient = null;

            this.receive_event_pool = new Stack<SocketAsyncEventArgs>(max_connections);
            this.send_event_pool = new Stack<SocketAsyncEventArgs>(max_connections);

            SocketAsyncEventArgs arg;
            for(int i=0;i<max_connections;i++)
            {
                {
                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(receive_completed);
                    arg.UserToken = null;
                    SetBuffer(arg);
                    this.receive_event_pool.Push(arg);
                }

                {
                    arg = new SocketAsyncEventArgs();
                    arg.Completed += new EventHandler<SocketAsyncEventArgs>(send_completed);
                    arg.UserToken = null;
                    arg.SetBuffer(null,0,0);
                    this.send_event_pool.Push(arg);
                }
            }
        }
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if(m_freeIndexPool.Count > 0)
            {
                args.SetBuffer(m_buffer, m_freeIndexPool.Pop(), m_bufferSize);
            }
            else
            {
                if((m_numBytes - m_bufferSize) < m_currentIndex)
                {
                    return false;
                }
                args.SetBuffer(m_buffer, m_currentIndex, m_bufferSize);
                m_currentIndex += m_bufferSize;
            }
            return true;
        }
        void receive_completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Receive)
            {
                chat_client_receive_completed(null, e);
                return;
            }
            throw new ArgumentException("The last operation completed on the socket was not a receive");
        }
        void send_completed(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                Token token = e.UserToken as Token;
            }
            catch (Exception)
            {

            }
        }

        //서버 시작
        public void start(string host, int  port, int backlog)
        {
            this.listen_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine(this.listen_socket);
            IPAddress address;
            if(host == "0.0.0.0")
            {
                address = IPAddress.Any;
            }
            else
            {
                address = IPAddress.Parse(host);
            }
            IPEndPoint endpoint = new IPEndPoint(address, port);
            this.callback_on_newclient += on_new_client;
            this.session_created_callback += add_users;
            roomManager_cs.start_roommanager(this);
            chatManager_cs.start_chatmanager(this);
            chatManager_cs.set_roomManager(roomManager_cs);
            try
            {
                listen_socket.Bind(endpoint);
                listen_socket.Listen(backlog);
                this.accept_event = new SocketAsyncEventArgs();
                this.accept_event.Completed += new EventHandler<SocketAsyncEventArgs>(on_accept_completed);

                Thread listen_thread = new Thread(do_listen);
                listen_thread.Start();
            }
            catch(Exception e)
            {

            }
        }
        public void do_listen()
        {
            Console.WriteLine("Start listen");
            this.flow_control_event = new AutoResetEvent(false);
            while(true)
            {
                this.accept_event.AcceptSocket = null;
                bool pending = true;
                try
                {
                    pending = listen_socket.AcceptAsync(this.accept_event);
                }
                catch(Exception e)
                {
                    continue;
                }
                if(!pending)
                {
                    on_accept_completed(null, this.accept_event);
                }
                
                this.flow_control_event.WaitOne();
            }
        }
        void on_accept_completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                Socket client_socket = e.AcceptSocket;
                this.client_sockets.Add(client_socket);
                Console.WriteLine("Client connected >> " + client_sockets.Count);

                // 클라이언트와의 비동기 통신 시작
                StartReceive(client_socket);
            }
            else
            {
                // 에러 처리
            }
            this.flow_control_event.Set();
            return;
        }
        void StartReceive(Socket clientSocket)
        {
            try
            {
                // 비동기 수신 작업 설정
                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
                byte[] buffer = new byte[1024];
                receiveArgs.SetBuffer(buffer, 0, buffer.Length);
                receiveArgs.UserToken = clientSocket;
                receiveArgs.Completed += ReceiveCompleted;

                // 비동기 수신 시작
                bool willRaiseEvent = clientSocket.ReceiveAsync(receiveArgs);
                if (!willRaiseEvent)
                {
                    // 동기적으로 완료됨, 직접 완료 처리
                    ReceiveCompleted(clientSocket, receiveArgs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("start receive >> Error starting receive: " + ex.Message);
                // 에러 처리
            }
        }
        void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            Socket clientSocket = (Socket)sender;
            try
            {
                bool islogon = false;
                int bytesReceived = e.BytesTransferred;
                if (bytesReceived > 0 && e.SocketError == SocketError.Success)
                {
                    byte[] buffer = e.Buffer;
                    string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                    accept_receive_data receivedInfo = JsonConvert.DeserializeObject<accept_receive_data>(receivedJson);
                    
                    PROTOCOL client_select_menu = receivedInfo.pt_id;

                    PROTOCOL pt_id = PROTOCOL.Setting;

                    login_success_info signup_login_state = new login_success_info();
                    // 수신한 데이터를 처리
                    if (client_select_menu == PROTOCOL.SIGNUP_Request)
                    {
                        bool isExistingUser = false;
                        foreach (login_info u in fake_user_db)
                        {
                            if (u.ID.Equals(receivedInfo.info.ID))
                            {
                                isExistingUser = true;
                                break;
                            }
                        }
                        if (!isExistingUser)
                        {
                            fake_user_db.Add(receivedInfo.info);
                            Console.WriteLine("Send Signup Access >> " + fake_user_db.Count);
                            pt_id = PROTOCOL.SIGNUP_Success;
                        }
                        else
                        {
                            Console.WriteLine("User with the same ID already exists");
                            pt_id = PROTOCOL.SIGNUP_Fail;
                        }
                    }
                    else if(client_select_menu == PROTOCOL.LOGIN_Request)
                    {
                        bool isexistuser = false;
                        foreach (login_info u in fake_user_db)
                        {
                            if (u.ID.Equals(receivedInfo.info.ID) && u.PW.Equals(receivedInfo.info.PW))
                            {
                                isexistuser = true;
                                break;
                            }
                        }
                        if (isexistuser)
                        {
                            Console.WriteLine("Send Login Access");
                            pt_id = PROTOCOL.LOGIN_Success;

                            //room_list_num 보내기
                            List<Room> client_room_list = new List<Room>();
                            client_room_list = roomManager_cs.Find_room_list(receivedInfo.info.ID);
                            Console.WriteLine(client_room_list.Count);
                            for(int i=0;i<client_room_list.Count;i++)
                            {
                                signup_login_state.room_nums.Add(client_room_list[i].room_num);
                                signup_login_state.room_names.Add(client_room_list[i].room_name);
                            }
                            islogon = true;
                        }
                        else
                        {
                            Console.WriteLine("Send Login Fail");
                            pt_id = PROTOCOL.LOGIN_Fail;
                        }
                    }
                    signup_login_state.pt_id = pt_id;
                    Console.WriteLine(signup_login_state);
                    string jsonData = JsonConvert.SerializeObject(signup_login_state);
                    byte[] send_buffer = Encoding.UTF8.GetBytes(jsonData);
                    clientSocket.Send(send_buffer);

                    Console.WriteLine("send accept client >> " + jsonData);
                    // 다음 비동기 수신 시작
                    if (islogon == false) StartReceive(clientSocket);
                    else this.callback_on_newclient(clientSocket, null, receivedInfo.info.ID);
                }
                else
                {
                    // 클라이언트가 연결을 종료한 경우
                    client_sockets.Remove(clientSocket);
                    clientSocket.Close();
                    Console.WriteLine("Client disconnected >> "+ client_sockets.Count);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ReceiveCompleted >> Error handling receive: " + ex.Message);
                // 에러 처리
            }
        }
        void on_new_client(Socket client_socket, object token, string client_ID)
        {
            Console.WriteLine("on_new_client");
            //멀티 스레딩에서 connected_count를 안전하게 증가시키는 역할
            //다른 스레드가 이 변수에 동시에 접근하여 값을 변경하는 것을 방지하고 원자적으로 증가
            Interlocked.Increment(ref this.connected_count);
            SocketAsyncEventArgs receive_args = this.receive_event_pool.Pop();
            SocketAsyncEventArgs send_args = this.send_event_pool.Pop();
            Token client_token = new Token(client_ID);
            client_token.on_session_closed += this.on_session_closed;
            receive_args.UserToken = client_token;
            send_args.UserToken = client_token;
            begin_receive(client_socket, receive_args, send_args);

            if (this.session_created_callback != null)
            {
                Console.WriteLine("add user");
                this.session_created_callback(client_token);
            }
            Console.WriteLine("connected_count >> "+ users.Count + "\n");

        }
        void begin_receive(Socket socket, SocketAsyncEventArgs receive_args, SocketAsyncEventArgs send_args)
        {
            Console.WriteLine("Chat Program Start");
            Token user_token = receive_args.UserToken as Token;
            user_token.set_event_args(send_args, receive_args);
            user_token.socket = socket;

            byte[] client_receive = new byte[1024];
            user_token.receive_event_args.SetBuffer(client_receive, 0, client_receive.Length);

            bool pending = socket.ReceiveAsync(receive_args);
            if(!pending)
            {
                chat_client_receive_completed(socket, receive_args);
            }
        }
        void chat_client_receive_completed(object sender, SocketAsyncEventArgs e)
        {
            Token user_token = e.UserToken as Token;
            try
            {
                Console.WriteLine("\nchat client receive completed");
                int bytesReceived = e.BytesTransferred;
                if (bytesReceived > 0 && e.SocketError == SocketError.Success)
                {
                    byte[] buffer = e.Buffer;
                    string receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                    room_create_message received_room_message = JsonConvert.DeserializeObject<room_create_message>(receivedJson);
                    Console.WriteLine(received_room_message.pt_id);
                    if (received_room_message.pt_id == PROTOCOL.Room_Create_Ready_Request || received_room_message.pt_id == PROTOCOL.Room_Create_Request || received_room_message.pt_id == PROTOCOL.Room_Enter_Request || received_room_message.pt_id == PROTOCOL.Room_Leave)
                    {
                        roomManager_cs.enqueue_room_message(user_token, received_room_message);
                    }
                    else if(received_room_message.pt_id == PROTOCOL.Send_Message)
                    {
                        chatManager_cs.enqueue_chat_message(user_token, received_room_message);
                    }
                    bool pending = user_token.socket.ReceiveAsync(e);
                    if (!pending) chat_client_receive_completed(sender, e);
                }
                else
                {
                    Console.WriteLine("Logon Client disconnected");
                    on_session_closed(user_token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("chat_client_receive_completed: Error handling receive: " + ex.Message);
                // 에러 처리
            }
        }
        public List<string> deliver_user_db(Token client_token)
        {
            List<string> all_users_IDs = new List<string>();
            foreach (var user in fake_user_db)
            {
                if(!user.ID.Equals(client_token.client_ID))
                {
                    Console.WriteLine("fake user db >> " + user.ID);
                    all_users_IDs.Add(user.ID);
                }
            }
            return all_users_IDs;
        }
        void SendDataToToken(Token token, string message)
        {
            try
            {
                byte[] messageBuffer = Encoding.UTF8.GetBytes(message);
                token.socket.Send(messageBuffer);
                Console.WriteLine("deliver message");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending data to token: " + ex.Message);
                // 에러 처리
            }
        }
        void add_users(Token token)
        {
            Console.WriteLine("on_session_created");
            lock(users)
            {
                this.users.Add(token);
            }
        }

        public List<Token> room_update_send(List<string> current_invited_IDs)
        {
            List<Token> deliver_current_token = new List<Token>();
            foreach(var current_user in users)
            {
                foreach(var invited_ID in current_invited_IDs)
                {
                    if(current_user.client_ID.Equals(invited_ID))
                    {
                        deliver_current_token.Add(current_user);
                        break;
                    }
                }
            }
            return deliver_current_token;
        }
        void on_session_closed(Token token)
        {
            Interlocked.Decrement(ref this.connected_count);
            Console.WriteLine("on_session_closed");
            lock (users)
            {
                this.users.Remove(token);
                if (this.receive_event_pool != null) this.receive_event_pool.Push(token.receive_event_args);
                if (this.send_event_pool != null) this.send_event_pool.Push(token.send_event_args);
                roomManager_cs.remove_current_token(token);
                token.token_close();
                Console.WriteLine("user connected_count >> " + users.Count + "\n");
            }
        }
    }
}