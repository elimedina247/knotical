# Knotical — working notes for Claude

## Ask before building or running

Do not launch the Unity editor, run `unity build`, `unity run`, `unity test`, `unity open`,
`unity projects create`, or anything else that compiles the project or starts the game
without being given permission first. Ask, say what the run would measure, and wait.

Once permission is given, iterate freely for that task: run, read the log, change, run
again. Measured numbers beat predicted ones — predictions have been wrong here before.

The Unity CLI is `%LOCALAPPDATA%\Unity\bin\unity.exe` (not on PATH). Pass `--no-banner
--non-interactive` in scripts. The editor is 6000.6.0f1 under
`C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Batch-mode runs need
`-logFile`; read the log, do not guess from exit codes.

Without permission, finish the edits, say what to look for when it runs, and stop there.

## Eli owns git

Never commit, push, tag, or stage unprompted. Finish the edits and say what to commit.

## Ask when the design is ambiguous

When a request leaves gameplay design open — which input triggers what, how two mechanics
share a button, which behavior wins when systems overlap — ask before building. One short
question listing the options. Do not invent a control scheme or pick an interaction model
on Eli's behalf; a guessed design costs more to unwind than the question would have.

## Prefab and scene overrides outrank code defaults

A value tuned on a prefab instance inside a scene is written into the `.unity` file as an
override. It beats the prefab and the C# field initializer, silently. The same trap cost a
session of tuning in the Godot version.

Before concluding a value has no effect, search the `.unity` and `.prefab` files for it.

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

Unity 6000.6, C#, URP. Co-op physics sailing game. Ported from Godot on 2026-09-11; the
Godot project is at git tag `godot-final`. The plan is `docs/plan-unity-port.md`.

Ocean and wind are both pure functions of time, which is what keeps them off the network.
`Ocean.cs` and the ocean shader implement the same wave maths on CPU and GPU — a change to
one is a change to both, or buoyancy stops matching what is drawn.
