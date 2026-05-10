# Documentation

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
NetworkManager Network;
void Start()
{
  Network.BindRPC<string>("rpcName", MyRPC);
}
```
Why is there <string> ?
You need to specify each parameter type in order.

Then to call the parameter do this:
```csharp
Network.RPC("rpcName", RPCTarget.Others, "Hello Guys!");
```
So "rpcName" is the name you set when you binded it.
RPCTarget.x is the who the RPC is going to, it has Master, AllByServer, All, Others
"Hello Guys!" is the parameter into MyRPC().

### Network Varaibles
