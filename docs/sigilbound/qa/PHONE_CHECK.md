# Phone check: one page for testers

We have never measured the game on a real phone. This sheet takes about **30 minutes** and gives us real
numbers. Please do it on each phone you have.

## 1. Install

- Open this link on the phone and install:
  https://github.com/eqtitan/SigilBound/releases/download/unity-latest/Sigilbound-Unity.apk
  If Android asks, allow installs from this source. If you already have it, the game updates itself.
- Start the game and check the build number at the bottom of **Settings**. You'll need it later.
- Charge the phone to **at least 50%** and let it cool down if it's warm. Close other apps.

## 2. Run the check at Auto quality

1. **Settings > Graphics quality > Auto.**
2. **Settings > Run phone check**, then **don't touch the screen for 60 seconds.**
   The game plays a set route by itself: Home, the team screen, a big storm-coast boss battle, the Titan
   arriving, and back. A bar at the top shows the time left. **Stop** cancels.
   Nothing is saved or spent: no rewards, no Titan try used, and your progress is untouched.
3. A summary appears and **the report is copied**. Paste it into your message to us straight away
   (WhatsApp, email or anything else). It's also saved for Send feedback.

## 3. Run it again at Low

**Settings > Graphics quality > Low**, then **Run phone check** again and paste that report too.
Then set quality back to **Auto**.

## 4. Play for 20 minutes

Play normally on Auto quality. Include:
- a few **campaign** fights, played by hand and on Auto;
- **Auto Climb** for at least 5 minutes (the Climb switch in battle);
- a **Titan** fight, or Titan practice;
- opening **Heroes** and spinning a hero in the viewer.

While you play, note:

| What | What to look for |
|---|---|
| **Heat** | Warm or hot? After how many minutes? Did the game turn the graphics down by itself? |
| **Touch** | Any button that's hard to hit, a tap that went to the wrong thing, or a scroll that fought you? |
| **Sound** | Crackles, sounds cutting out, music too loud or quiet, anything playing at the wrong moment? |
| **Cloth and hair** | Coats, capes and robes should swing and settle. Do any jitter, stretch, fly off or pass through legs? |
| **Shaders and looks** | Black or pink surfaces, flickering, heroes too dark or shiny, missing glow, stand-in blocks that stay? |
| **Smoothness** | Any freeze or stutter you could feel? Say when (opening a screen, a hero appearing, a big attack). |
| **Battery** | The battery % at the start and end of the 20 minutes. |

## 5. Send it

1. **Settings > Run phone check** once more at the end, while the phone is warm. This third report shows
   how the phone copes when hot.
2. **Settings > Send feedback.** Type your notes from step 4, leave **"Attach the last phone check"** ticked,
   tap **Copy report** and paste it to us.
3. A screenshot of anything that looked wrong helps a lot.

**In short, please send:** your phone model, three phone-check reports (Auto, Low, Auto when warm) and
your notes. Thank you!

---
_For the developer: the report format, the route and how it's measured are in `unity/Assets/Sigilbound/Core/PhoneCheck.cs`
and `Client/PhoneCheckRun.cs` (PLAN step 116). Compare the `all:` and `steps:` lines across phones and builds.
Each hitch line gives the step and the SBPERF line logged in the same frame._
