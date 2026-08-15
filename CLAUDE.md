# Knotical — working notes for Claude

## Ask before building or running

Do not run `dotnet build`, `msbuild`, the Godot CLI, or anything else that compiles or
launches the game without being given permission first. Ask, say what the run would
measure, and wait.

Once permission is given, iterate freely for that task: build, run headless with
`--quit-after`, read the trace, change, run again. Measured numbers beat predicted ones —
predictions have been wrong here before.

Godot lives at
`C:\Users\elime\Downloads\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe`.
Use the `_console` build or stdout is lost. `dotnet build` first or the run uses a stale
assembly and the code appears not to have changed.

Without permission, finish the edits, say what to look for when it runs, and stop there.

## Scene overrides outrank code defaults

`ocean.tscn` instances `boat_2.tscn`, and any property tuned while `ocean.tscn` is open is
written there as an instance override. Those beat both `boat_2.tscn` and the C# defaults,
silently. This has already cost a session's worth of tuning.

Before concluding a value has no effect, grep `ocean.tscn` for it.

## Do not comment the code

Write code without comments. No inline comments, no XML doc comments, no explanatory
headers. Names and structure carry the meaning.

Explanation belongs in the chat response, not in the file. If a decision needs justifying,
say it in the reply.

Leave existing comments alone unless the task is specifically to change them.

## Keep replies short

Lead with what changed and the one number that matters. A few sentences, or a small table
when comparing options. No multi-section write-ups, no restating the reasoning chain, no
listing the alternatives that were rejected.

Report the working, not the workings: give the conclusion and the evidence for it, and drop
the derivation unless asked. If a caveat matters, one line.

## Explain the sailing terms

Eli does not sail. Any nautical or naval-architecture term gets a short plain-English gloss
the first time it appears in a reply — in brackets, one clause, no lecture.

"heel (how far it leans sideways)", "leeway (sliding sideways instead of going where the
bow points)", "beam (width)", "draft (how deep it sits)", "close hauled (sailing as close
to the wind as it can)".

Prefer the plain word where one exists. Say "width" not "beam" when nothing is lost.

## Project

Godot 4.7, C# / .NET, Forward+, Jolt physics. Co-op physics sailing game.

Ocean and wind are both pure functions of time, which is what keeps them off the network.
`Ocean.cs` and `ocean.gdshader` implement the same wave maths on CPU and GPU — a change to
one is a change to both, or buoyancy stops matching what is drawn.
