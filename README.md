# EasyNet-for-Unity
My work in progress Multiplayer solution for Unity

# Server

Its basically plug and play, all you need to do is sort out a self hosting solution, like Digital Ocean, Oracle, or your own pc.

# Client

Things like user made RPCs are comming and Network Variables are underdevelopement, they work but are very weird with updating.

The only working thing is the Network Transform Conpoennt which consistantly updates the transform of the player, all you need to do in ensure you have a NetowrkManager game obeject with the NetowrkManager.cs Conponent attached, then in the first list in the conponent, like NetworkObjects, put a prefab for your player, your player should include a NetworkView (NetworkBehaviour.cs - will be changed to NetworkView.cs) conponent and probably a NetworkTransform component aswell.



Over time, more features will be added. As the current features are obviously not enough for a functioning multiplayer game.



# Whats the goal?

To make a basic Mutliplayer engine that 'works' so noone is trapped from CCU Limits. Obviously there are much better alternatives than this but its a fun project I'm working on.
