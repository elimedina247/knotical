# Knotical — working notes for Claude

## Never build the project

Do not run `dotnet build`, `dotnet run`, `msbuild`, the Godot CLI, or any other command
that compiles or launches the game. Eli builds and runs it. Finish the edits, say what
changed, and stop there.

If a change needs verifying, say what to look for when it runs — do not verify it yourself
by building.

## Do not comment the code

Write code without comments. No inline comments, no XML doc comments, no explanatory
headers. Names and structure carry the meaning.

Explanation belongs in the chat response, not in the file. If a decision needs justifying,
say it in the reply.

Leave existing comments alone unless the task is specifically to change them.

## Project

Godot 4.7, C# / .NET, Forward+, Jolt physics. Co-op physics sailing game.

Ocean and wind are both pure functions of time, which is what keeps them off the network.
`Ocean.cs` and `ocean.gdshader` implement the same wave maths on CPU and GPU — a change to
one is a change to both, or buoyancy stops matching what is drawn.
