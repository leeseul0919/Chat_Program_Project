using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chat_Server
{
    public enum PROTOCOL : short
    {
        Setting = 0,

        SIGNUP_Request = 1,
        SIGNUP_Success = 2,
        SIGNUP_Fail = 3,

        LOGIN_Request = 4,
        LOGIN_Success = 5,
        LOGIN_Fail = 6,

        Room_Create_Ready_Request = 7,
        Room_Create_Ready_Success = 8,
        Room_Create_Ready_Fail = 9,

        Room_Create_Request = 10,
        Room_Create_Success = 11,
        Room_Create_Fail = 12,

        Room_Enter_Request = 13,
        Room_Enter_Success = 14,
        Room_Enter_Fail = 15,

        Send_Message = 16,
        Room_Leave = 17,
        Room_Leave_Success = 18,
        Deliver_Message = 19,

        Room_Update = 20
    }
}
