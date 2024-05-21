# Chat_Program_Project

## SignUp/Login
```
[Client Send] ID, PW, SignUP Request
[Server Send] SingUp Success / Signup Fail
[Client Send] ID, PW, Login Request
[Server Send] Login Success, ID's Room info / Login Fail
```

## Room Create/Enter Room
```
[Client Send] Room Create Ready Request
[Server Send] Room Create Ready Success, All Users' IDs / Room Create Ready Fail
[Client Send] Room Create Request, Room Name, Invite IDs
[Server Send] Room Create Success / Room Create Fail
[Client send] Enter Room Request, Room num
[Server Send] Enter Room Success, Room num, Room messages / Enter Room Fail
```

## Send/Receive Message
```
[Client Send] Send Message, Room num, Send Message
[Server Send] Delive Message, Send Message
```

## Update Room
```
if some clients send 'create room' to server,
[Server Send] Room Update, Room num, Room Name
```
