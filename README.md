
## Dune

Dune is a project which attempts to allow running untrusted C# code at native speeds. It works by enforcing a whitelist of allowed types before and after compiling the untrusted code. It also provides utilities useful for implementing scripting systems into a project. It is not yet completed, but the whitelist enforcement should* work.

\* I made Dune for use in my personal project (a game engine), and it is currently very untested. I beileve it to be secure, but I could be very wrong about that. Use at your own risk.

I have additional plans to include hooking capabilities into Dune. I envision it being an alternitive to MonoMod with the addition of providing sandboxing for mods, but that dream is very far off.