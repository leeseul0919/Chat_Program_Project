using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Newtonsoft.Json;

namespace Chat_Client
{
	public class login_success_info
	{
		public PROTOCOL pt_id { get; set; }
		public List<int> room_nums = new List<int>();
		public List<string> room_names = new List<string>();
	}
	class Connector
	{
		class login_info
		{
			public string ID { get; set; }
			public string PW { get; set; }
		}
		class accept_send
        {
			public PROTOCOL pt_id { get; set; }
			public login_info info { get; set; }
        }
		
		public delegate void ConnectedHandler(Token token);
		public ConnectedHandler connected_callback { get; set; }
		login_info login_id_pw;
		Socket client_token;
		public SocketAsyncEventArgs send_arg;
		public SocketAsyncEventArgs receive_arg;

		public Token client_chat_manager;
		public List<int> room_list_num;
		public Connector()
		{
			this.connected_callback = null;
			login_id_pw = new login_info();
			room_list_num = new List<int>();
		}
		SocketAsyncEventArgs event_arg;
		public void connect(IPEndPoint remote_endpoint)
		{
			this.client_token = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.client_token.NoDelay = true;

			// 비동기 접속을 위한 event args
			event_arg = new SocketAsyncEventArgs();
			event_arg.Completed += on_connect_completed;
			event_arg.RemoteEndPoint = remote_endpoint;
			bool pending = this.client_token.ConnectAsync(event_arg);
			if (!pending)
			{
				on_connect_completed(null, event_arg);
			}
		}
		int accept_menu;
		string ID;
		void on_connect_completed(object sender, SocketAsyncEventArgs e)
		{
			if (e.SocketError == SocketError.Success)
			{
				//accept 상태일 때 시뮬레이션
				//accept 상태일 때는 회원가입/로그인만
				Console.WriteLine("SocketError Success");
				if (this.connected_callback != null)
				{
					//회원가입, 로그인 중에 사용할 기능 고르고
					do
					{
						Console.Write("1) 회원가입 2) 로그인 >> ");
						accept_menu = Console.Read() - '0';
						Console.WriteLine("you select >> " + accept_menu);
						if (accept_menu != 1 && accept_menu != 2)
						{
							Console.WriteLine("잘못된 기능을 선택했습니다. 다시 선택해주세요.");
							Console.ReadLine();
						}
					} while (accept_menu != 1 && accept_menu != 2);

					//보낼 데이터들 정의
					accept_send send_data_info = new accept_send();
					login_info info = new login_info();
					Console.Write("ID >> ");
					Console.ReadLine();
					info.ID = Console.ReadLine();   //ID 입력받고
					ID = info.ID;
					Console.Write("PW >> ");
					info.PW = Console.ReadLine();	//PW 입력받고

					if (accept_menu == 1)	//회원가입 이면
                    {
						send_data_info.pt_id = PROTOCOL.SIGNUP_Request;	//SIGNUP_Request 프로토콜
						send_data_info.info = info;

					}
					else if(accept_menu == 2)	//로그인 이면
                    {
						send_data_info.pt_id = PROTOCOL.LOGIN_Request;	//LOGIN_Request 프로토콜
						send_data_info.info = info;
					}

					//accept_send_info 클래스형 전송 데이터를 json으로 변환해서 보내기
					string info_json = JsonConvert.SerializeObject(send_data_info);
					Console.WriteLine(info_json);
					byte[] info_data = Encoding.UTF8.GetBytes(info_json);
					client_token.Send(info_data);
					
					//보내고 나서는 서버 응답 기다리는 비동기 수신 시작
					StartReceive(client_token);
				}
			}
			else
			{
				Console.WriteLine(string.Format("Failed to connect. {0}", e.SocketError));
			}

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
				Console.WriteLine("Error starting receive: " + ex.Message);
				// 에러 처리
			}
		}
		void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
		{
			Socket clientSocket = (Socket)sender;
			try
			{
				int bytesReceived = e.BytesTransferred;
				if (bytesReceived > 0 && e.SocketError == SocketError.Success)
				{
					byte[] buffer = e.Buffer;
					string received_state = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
					login_success_info receivedInfo = JsonConvert.DeserializeObject<login_success_info>(received_state);
					PROTOCOL protocol = receivedInfo.pt_id;	//받은 프로토콜이

					if(accept_menu == 1)	//회원가입 일때는
                    {
						if (protocol == PROTOCOL.SIGNUP_Success) Console.WriteLine("Receive Signup Success\n");	//SIGNUP_Success받으면 지금은 일단 상태 출력만
						else Console.WriteLine("Receive Signup Fail\n");	//SIGNUP_Success를 못 받았을 때도 일단 출력만
						on_connect_completed(null, event_arg);	//다시 회원가입/로그인 기능 선택으로 돌아가기
					}
					else if(accept_menu == 2)	//로그인 일때는
                    {
						if (protocol == PROTOCOL.LOGIN_Success) //받은 값이 LOGIN_Success라면
						{
							Console.WriteLine("Receive LOGIN_Success\n");
							this.client_chat_manager = new Token(ID);
							this.client_chat_manager.on_session_closed += this.socket_close;
							this.client_chat_manager.set_event_args();
							this.client_chat_manager.socket = clientSocket;
							this.client_chat_manager.set_room_list(receivedInfo);
							this.client_chat_manager.start_chat();  //채팅 시작
							this.connected_callback(this.client_chat_manager);  //연결된 서버를 서버 리스트에 올리기
						}
						else
						{
							Console.WriteLine("Receive LOGIN_Fail\n");
							on_connect_completed(null, event_arg); //LOGIN_Success를 못 받았으면 다시 기능 선택부터 하러
						}
					}
				}
				else
				{
					// 클라이언트가 연결을 종료한 경우
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error handling receive: " + ex.Message);
				// 에러 처리
			}
		}
		public void connector_close()
        {
			client_chat_manager.token_close();
		}
		public void socket_close(Token token)
		{
			Console.WriteLine("socket close");
			token.token_close();
		}
	}
}
