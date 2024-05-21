# Chat_Program_Project

## SignUp/Login
```
[Client Send] ID/PW, SignUP Request

[Server Send] SingUp Success/Signup Fail receive

[Client Send] ID/PW, Login Request

[Server Send] Login Success, ID's Room info
```

## Room Create/Enter Room
1. <- Room Create Ready Request
2. Room Create Ready Success, All Users' IDs ->
3. <- Room Create Request, Room Name, Invite IDs
4. Room Create Success ->
5. <- Enter Room Request, Room num
6. Enter Room Success, Room num, Room messages ->


## Send/Receive Message
1. <- Send Message, Room num, Send Message
2. Delive Message, Send Message ->


## Update Room
1. Room Update, Room num, Room Name ->
