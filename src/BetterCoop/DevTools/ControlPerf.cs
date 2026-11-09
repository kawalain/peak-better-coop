using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BetterCoop.DevTools;

/// <summary>
/// Measures what the mod itself costs each frame: every Update of its own
/// components and every Harmony patch method it adds to the game, timed and
/// with what it allocates, from the first time it is asked on.
/// </summary>
internal static class ControlPerf
{
    private sealed class Tally
    {
        public long Ticks;

        public long Bytes;

        public int Calls;
    }

    private static readonly Dictionary<MethodBase, Tally> Tallies = new();

    private static bool measuring;

    private static bool instrumented;

    private static bool bytesWork = true;

    private const float MostSeconds = 15f;

    public static IEnumerator Measure(JObject args, Action<JToken> reply)
    {
        float seconds = Mathf.Clamp((float?)args["seconds"] ?? 5f, 0.5f, MostSeconds);
        Instrument();
        lock (Tallies)
        {
            Tallies.Clear();
        }

        int firstFrame = Time.frameCount;
        float start = Time.realtimeSinceStartup;
        measuring = true;
        yield return new WaitForSecondsRealtime(seconds);
        measuring = false;
        int frames = Math.Max(1, Time.frameCount - firstFrame);
        float elapsed = Time.realtimeSinceStartup - start;
        double tickMs = 1000.0 / Stopwatch.Frequency;
        List<KeyValuePair<MethodBase, Tally>> rows;
        lock (Tallies)
        {
            rows = Tallies.OrderByDescending(t => t.Value.Ticks).ToList();
        }

        reply(new JObject
        {
            ["frames"] = frames,
            ["fps"] = Math.Round(frames / elapsed, 1),
            ["modMsPerFrame"] = Math.Round(rows.Sum(r => r.Value.Ticks) * tickMs / frames, 4),
            ["modBytesPerFrame"] = bytesWork ? Math.Round(rows.Sum(r => (double)r.Value.Bytes) / frames, 1) : null,
            ["top"] = new JArray(rows.Take(20).Select(r => new JObject
            {
                ["method"] = $"{r.Key.DeclaringType?.Name}.{r.Key.Name}",
                ["msPerFrame"] = Math.Round(r.Value.Ticks * tickMs / frames, 4),
                ["callsPerFrame"] = Math.Round(r.Value.Calls / (double)frames, 2),
                ["bytesPerFrame"] = bytesWork ? Math.Round(r.Value.Bytes / (double)frames, 1) : null,
            })),
        });
    }

    /// <summary>Wraps each Update of the mod's components and each of its Harmony patch methods, once.</summary>
    private static void Instrument()
    {
        if (instrumented)
        {
            return;
        }

        instrumented = true;
        var harmony = new Harmony("in.kawala.bettercoop.perf");
        var before = new HarmonyMethod(typeof(ControlPerf).GetMethod(nameof(Before), BindingFlags.NonPublic | BindingFlags.Static));
        var after = new HarmonyMethod(typeof(ControlPerf).GetMethod(nameof(After), BindingFlags.NonPublic | BindingFlags.Static));
        foreach (MethodInfo method in Targets())
        {
            try
            {
                harmony.Patch(method, prefix: before, postfix: after);
            }
            catch (Exception exception)
            {
                Plugin.Verbose($"Perf could not time {method.DeclaringType?.Name}.{method.Name}: {exception.Message}");
            }
        }
    }

    private static IEnumerable<MethodInfo> Targets()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
        {
            if (type.Namespace == typeof(ControlPerf).Namespace || type.IsGenericTypeDefinition)
            {
                continue;
            }

            bool component = typeof(MonoBehaviour).IsAssignableFrom(type);
            foreach (MethodInfo method in type.GetMethods(all))
            {
                if (method.IsAbstract || method.IsGenericMethodDefinition)
                {
                    continue;
                }

                bool frameMethod = component && !method.IsStatic && method.Name is "Update" or "LateUpdate" or "FixedUpdate";
                bool patchMethod = method.IsStatic && method.GetCustomAttributes(true).Any(a => a is HarmonyPrefix or HarmonyPostfix or HarmonyFinalizer);
                if (frameMethod || patchMethod)
                {
                    yield return method;
                }
            }
        }
    }

    private static void Before(out (long Ticks, long Bytes) __state)
    {
        __state = measuring ? (Stopwatch.GetTimestamp(), Allocated()) : (0, 0);
    }

    private static void After(MethodBase __originalMethod, (long Ticks, long Bytes) __state)
    {
        if (!measuring || __state.Ticks == 0)
        {
            return;
        }

        long ticks = Stopwatch.GetTimestamp() - __state.Ticks;
        long bytes = Allocated() - __state.Bytes;
        lock (Tallies)
        {
            if (!Tallies.TryGetValue(__originalMethod, out Tally? tally))
            {
                Tallies[__originalMethod] = tally = new Tally();
            }

            tally.Ticks += ticks;
            tally.Bytes += Math.Max(0, bytes);
            tally.Calls++;
        }
    }

    private static long Allocated()
    {
        if (!bytesWork)
        {
            return 0;
        }

        try
        {
            // Unity's Mono counts no allocations per thread; the heap in use grows by what is allocated until a collection.
            return UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
        }
        catch (Exception)
        {
            bytesWork = false;
            return 0;
        }
    }
}
