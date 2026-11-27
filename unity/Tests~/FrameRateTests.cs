// Frame rate (owner decision 2026-11-27): 60 by default, the screen's 90/120 Hz as a choice, 30 for
// auto-replay, Low and a warm phone.
using System;
using Sigilbound.Core;

static class FrameRateTests
{
    public static void Run(Action<bool, string> check)
    {
        check(FrameRate.Mode(null) == "60" && FrameRate.Mode("") == "60" && FrameRate.Mode("auto") == "60", "fps: the default (and the old Auto) is 60");
        check(FrameRate.Mode("high") == "high" && FrameRate.Mode("30") == "30" && FrameRate.Mode("60") == "60", "fps: saved choices are kept");
        check(FrameRate.Target("60", 120, 2, false, false) == 60 && FrameRate.Target("60", 50, 2, false, false) == 50, "fps: 60 on a 120 Hz phone (the screen's rate if lower)");
        check(FrameRate.Target("high", 120, 2, false, false) == 120 && FrameRate.Target("high", 90, 2, false, false) == 90 && FrameRate.Target("high", 144, 3, false, false) == 120, "fps: the high choice is the screen's 90 or 120");
        check(FrameRate.Target("high", 120, 2, false, false, 60) == 60, "fps: high steps down to 60 when the phone can't hold it");
        check(FrameRate.Target("high", 120, 3, false, true) == 30 && FrameRate.Target("60", 120, 2, false, true) == 30, "fps: auto-replay runs at 30 whatever the choice");
        check(FrameRate.Target("high", 120, 0, false, false) == 30 && FrameRate.Target("60", 120, 2, true, false) == 30 && FrameRate.Target("30", 120, 2, false, false) == 30, "fps: Low, a warm phone and the battery choice run at 30");
    }
}
