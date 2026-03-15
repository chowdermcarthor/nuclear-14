// #Misfits Change - Reworked to use IGameTiming for deterministic, jitter-free day/night cycle
// Time is computed from absolute game time on the client; no per-frame dirty calls.
using System.Linq;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Shared._NC14.DayNightCycle
{
    public sealed class DayNightCycleSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DayNightCycleComponent, MapInitEvent>(OnMapInit);
        }

        private void OnMapInit(EntityUid uid, DayNightCycleComponent component, MapInitEvent args)
        {
            if (component.TimeEntries.Count < 2)
            {
                // Default Fallout-inspired color cycle
                component.TimeEntries = new List<TimeEntry>
                {
                    new() { Time = 0.00f, ColorHex = "#060610" }, // Midnight       – #Misfits Tweak: darkened from #121224 for a proper pitch-black feel
                    new() { Time = 0.04f, ColorHex = "#0C0C1C" }, // Very early night – #Misfits Tweak: darkened from #18182D to match new midnight
                    new() { Time = 0.08f, ColorHex = "#4A3420" }, // Early dawn      – first warm hint
                    new() { Time = 0.17f, ColorHex = "#7A5C34" }, // Dawn            – amber glow
                    new() { Time = 0.25f, ColorHex = "#A87448" }, // Sunrise         – warm orange
                    new() { Time = 0.33f, ColorHex = "#D4A85C" }, // Early morning   – golden
                    new() { Time = 0.42f, ColorHex = "#E8C070" }, // Mid-morning     – bright gold
                    new() { Time = 0.50f, ColorHex = "#F8D880" }, // Noon            – peak brightness, warm white-gold
                    new() { Time = 0.58f, ColorHex = "#F0C870" }, // Early afternoon – slightly softer
                    new() { Time = 0.67f, ColorHex = "#CCA050" }, // Late afternoon  – deepening gold
                    new() { Time = 0.75f, ColorHex = "#B07840" }, // Sunset          – warm orange
                    new() { Time = 0.83f, ColorHex = "#7A4A2C" }, // Dusk            – deep amber-red
                    new() { Time = 0.92f, ColorHex = "#1C1430" }, // Early night     – #Misfits Tweak: darkened from #241B38 to smooth into new midnight
                    new() { Time = 1.00f, ColorHex = "#060610" }  // Back to Midnight – #Misfits Tweak: darkened from #121224
                };
            }
        }

        /// <summary>
        /// Returns the interpolated ambient light color for <paramref name="time"/> (0–1 normalized
        /// position within the cycle). Used by the client-side rendering system.
        /// </summary>
        public static Color GetInterpolatedColor(DayNightCycleComponent component, float time)
        {
            var entries = component.TimeEntries;

            for (int i = 0; i < entries.Count - 1; i++)
            {
                if (time >= entries[i].Time && time <= entries[i + 1].Time)
                {
                    var t = (time - entries[i].Time) / (entries[i + 1].Time - entries[i].Time);
                    return InterpolateHexColors(entries[i].ColorHex, entries[i + 1].ColorHex, t);
                }
            }

            // Wrap between the last and first entry
            var lastEntry = entries.Last();
            var firstEntry = entries.First();
            var wrappedT = (time - lastEntry.Time) / (1f + firstEntry.Time - lastEntry.Time);
            return InterpolateHexColors(lastEntry.ColorHex, firstEntry.ColorHex, wrappedT);
        }

        private static Color InterpolateHexColors(string hexColor1, string hexColor2, float t)
        {
            var color1 = Color.FromHex(hexColor1);
            var color2 = Color.FromHex(hexColor2);
            return new Color(
                color1.R + (color2.R - color1.R) * t,
                color1.G + (color2.G - color1.G) * t,
                color1.B + (color2.B - color1.B) * t);
        }
    }
}