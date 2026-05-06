# EasyNet-for-Unity
My work in progress Multiplayer solution for Unity

# Server
Just make a new C# project in VS Studio 2026 or whatever, and add EasyNet.cs and _ServerTerminal.cs into the project, run/compile it and it should say EasyNet Unity Server, if it says DO NOT TURN OFF LOADING CLIENTS, ignore that, its fine to shut down if it says that. I will remove this pointless line next time.
# Client
Things like user made RPCs are comming and Network Variables are underdevelopement, they work but are very weird with updating.

The only working thing is the Network Transform Conpoennt which consistantly updates the transform of the player, all you need to do in ensure you have a NetowrkManager game obeject with the NetowrkManager.cs Conponent attached, then in the first list in the conponent, like NetworkObjects, put a prefab for your player, your player should include a NetworkView (NetworkBehaviour.cs - will be changed to NetworkView.cs) conponent and probably a NetworkTransform component aswell.

# Note
Over time, more features will be added. As the current features are obviously not enough for a functioning multiplayer game.

# Whats the goal?
To make a basic Mutliplayer engine that 'works' so noone is trapped from CCU Limits. Obviously there are much better alternatives than this but its a fun project I'm working on.
