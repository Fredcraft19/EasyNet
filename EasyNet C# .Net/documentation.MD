# Documentation
For all code snippets, they most likely are dependent on these:
```cs
// For NetworkManager
using EasyNet.Manager;

// For Network
using EasyNet.View;

// For C#'s built in IPAddress
using System.Net;

// For Debugging
using EasyNet_Debugging;
```
## Debugging
To debug EasyNet for C# .Net simply add these 2 lines in your code and it will start to debug whatever you ask for:
```cs
using EasyNet_Debugging;
debug.debugmode = DebugMode.All;

// Go do Debugging.cs for all DebugMode options
// Examples: Logs, Warnings, Errors and All
```
## Connectivity
### Initializing the Client
To start connecting to the server and all that, first you must inititalize your client by:
```cs
NetworkManager.Initialize(IPAddress.Loopback, 8080, true);
// What are all these parameters for?!
// IPAddress.Loopback is an example IP, you would replace it with your servers IP or keep it as loopback if the server is running on your client.
// 8080 is an example Port your server will be running on.
// True means to auto connect to the server (bypassing 'NetworkManager.Connect()')
```
### Connection to Server
To connect to your EasyNet server* you must use:

*You must host your own EasyNet server for this to work. Its recommended to use an old laptop or a server from Google Cloud or Oracle for example (these do come with free tiers / trials), this should then be running the latest released console app in the Relaeses tab. (ignore it if it says EasyNet for Unity or something, the servers work for .Net and Unity both. Just not at the same time (techincally do but it will cause very weird errors))
```cs
NetworkManager.Connect();
```
This tells the server that you (the client) exist and are ready to join a room.

### Rooms
To join/leave a room, just do this:

```cs
// Joining
NetworkManager.JoinRoom("ROOM NAME HERE");

// To Leave
NetworkManager.LeaveRoom();
```

## RPCs
### Creating & Binding
These are basically identical to EasyNet for Unity.
```cs
// Creating the RPC
// Just make a normal method with a void return type and any parameters
void Message(string sender, string msg
{
  Console.WriteLine($"{sender}: '{msg}'");
}

// To bind it:
Network.BindRPC<string, string>("message", Message);

// Why '<string, string>'?
// You can see that the Message method has 2 parameters, that are both string, so you put the parameter types in withy BindRPC. They must be in order though.
// What does "message" do ? "message" is the RPC name. it can literally be anything you want.
```
### Calling
To call a RPC:
```cs
Network.RPC("message", RPCTarget.Others, "Bob", "Hey guys, its bob!");

// Why "message"? THis is the RPC name you set in BindRPC.
// Why RPCTarget.Others? This sends it to all the other clients except yourself.
// Why "Bob?" Thats the first parameter of 'Message' method, this gets passed into the method.
// Why "Hey guys..."? Thats the second parameter of 'Message' method, this gets passed into the method.
```



