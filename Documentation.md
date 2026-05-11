# Documentation

### Connecting to the server
First, make sure you have a server.
You need EasyNet.cs and _ServerTerminal.cs to be compiled into a C# Console App. Either through Visual Studio 2026 or other ways.
Then run the console app.
It should say EasyNet Unity Server.
It wont do anything unitl a client speaks to it.
It is recomended for the server to be ran on an old laptop, server, or your PC (for testing)

Once you have the server.
Connect to the server with:
```csharp
NetworkManager Network;
Network.Connect();
```

Join a room with:
```csharp
Network.JoinRoom("Room Name");
```

### RPCs
How to create and use RPC's?
First, in any game object. Create your method, make sure its no return type. Like this:
```csharp
void MyRPC(string message)
{
  Debug.Log($"Message received! {message}");
}
```
Then you have to bind it. Make sure its binded before the RPC can ever be called. Like in Awake() or Start()
```csharp
NetworkView Network;  // Network View Reference
void Start()
{
  Network.BindRPC<string>("rpcName", MyRPC);
}
```
Why is there string specified for the BindRPC?
You need to specify each parameter type in order.

Then to call the parameter do this:
```csharp
Network.RPC("rpcName", RPCTarget.Others, "Hello Guys!");
```
So "rpcName" is the name you set when you binded it.
RPCTarget.x is the who the RPC is going to, it has Master, AllByServer, All, Others
"Hello Guys!" is the parameter into MyRPC().

### Network Varaibles
How to create and use Network-Synced Variables?

First, create and register the variable with:
```csharp
NetworkView Network;  // Network View Reference
Network.RegisterVariable(new NetworkVariable<string>(Network, "message");
```

To update it use:
```csharp
Network.NetworkVariable["message"].SetValue("Network-Synced Message!");
```

To Read it use:
```csharp
Network.GetVariable<string>("message");
```
Why is there <string>?
You need to specify the variable type then it can return it.

###  Network Conponents

To use a network conponent. Ensure there is a Network Manager in your scene.

Get your game object and add conponents:
Network Behaviour and Network Transform (or any conponent) to the game object.
Make sure the object is Spawned in and not already in the scene as that will cause weird ownership between clients.

I've made sure to keep the Network Conponents like Transform as simple and easy as possible. Literally no code required.

### Network Object
To spawn a Network Object, turn a game object into a prefab somewhere in your assets folder. Then in NetworkManager, add it to the NetworkObjects.
If you want to use NetworkManagers .SpawnPlayer() then ensure your player prefab is the first element in your NetworkObjects list (make sure its Element 0).

How to spawn the object in for use?
```csharp
NetworkManager Network;  // Network Manager Reference
Network.Instantiate("ObjectName");     // Spawn by Object Name
Network.Instantiate(1)                 // Spawn by Object ID
```

Whatever client runs Network.Instatiate owns the object.

If the client leaves. Their spawned objects should disapear but in future, it could be in NetworkBehaviour and to give it settings like: Destroy after owner leaves, if this is off, the owner will be changed. Right now I am focused on making this work fully than adding more features.
