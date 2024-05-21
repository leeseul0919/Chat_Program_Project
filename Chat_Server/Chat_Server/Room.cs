using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chat_Server
{
    class Room
    {
        public int room_num;
        public string room_name;
        public List<string> room_users;
        public List<Token> current_contact_users;
        public List<string> room_messages;

        public Room(int room_num, string room_name, List<string> IDs)
        {
            this.room_num = room_num;
            this.room_name = room_name;
            room_users = IDs;
            current_contact_users = new List<Token>();
            room_messages = new List<string>();
        }
    }
}
