This repository contains a small demo of a quake-style multiplayer FPS that integrates a number of modern netcode techniques for quality of gameplay.

**Features**:

* Client-side prediction of player entities
* Client-side interpolation of remote entities
* Backwards reconciliation and replay
* Real-time adjustment of client simulation speed to optimize server's input buffer (Overwatch's method).
* (TODO) Server-side lag compensation
* (TODO) Hitscan weapons
* (TODO) Projectile weapons
* Master server to manage discovery and connection (via [hotel](https://github.com/minism/hotel))

**References & Research**:

* [Quake 3 Networking Code Review](http://fabiensanglard.net/quake3/network.php)
* [Client-Side Prediction With Physics in Unity](http://www.codersblock.org/blog/client-side-prediction-in-unity-2018)
* [Gaffer On Games Networking](https://gafferongames.com/tags/networking/)
* [Unreal Networking Overview](https://docs.unrealengine.com/udk/Three/NetworkingOverview.html)
* [Gabriel Gambetta Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html)
* [Overwatch Gameplay Architecture & Netcode](https://www.youtube.com/watch?v=W3aieHjyNvw)
* [TinyBirdNet](https://github.com/Saishy/TinyBirdNet-Unity)
* https://www.gamedev.net/forums/topic/696756-command-frames-and-tick-synchronization/

** Architecture **:

TODO
