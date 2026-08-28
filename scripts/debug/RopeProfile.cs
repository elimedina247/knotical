using Godot;
using System.Diagnostics;

namespace Knotical.Debug;

public static class RopeProfile
{
    public static bool Enabled;

    private static readonly long[] Ticks = new long[8];
    private static readonly long[] Calls = new long[8];
    private static readonly string[] Names =
    {
        "route", "relax", "collide", "depenetrate", "constrain", "skin", "spine", "verts",
    };

    private static long _reported;

    public static long Start() => Enabled ? Stopwatch.GetTimestamp() : 0L;

    public static void Stop(int slot, long from)
    {
        if (!Enabled || from == 0L) return;

        Ticks[slot] += Stopwatch.GetTimestamp() - from;
        Calls[slot]++;
    }

    public static void Count(int slot, long amount)
    {
        if (!Enabled) return;

        Ticks[slot] += amount;
        Calls[slot]++;
    }

    public static void Report(float seconds)
    {
        if (!Enabled) return;

        long now = Stopwatch.GetTimestamp();
        if (_reported == 0L) _reported = now;

        string row = "";
        for (int i = 0; i < Names.Length; i++)
        {
            if (Calls[i] == 0) continue;

            double ms = Ticks[i] * 1000.0 / Stopwatch.Frequency / seconds;
            row += i >= 6
                ? $" {Names[i]}={Ticks[i] / Calls[i]}"
                : $" {Names[i]}={ms:0.00}ms";
        }

        GD.Print($"ropeprofile:{row}");

        System.Array.Clear(Ticks);
        System.Array.Clear(Calls);
    }
}
