"""Re-balance hand-written heroes after a combat rule change (PLAN step 97).

    python3 docs/sigilbound/retune.py id [id ...]

Each hero is one line in Core/GameData.cs. Its power is scaled by a factor f applied to every skill
multiplier (the 3rd argument of S(...)) and every Heal(x) / Shield(x) amount, from the line as it was
when the script started. f is bisected with `dotnet run --project unity/Tests~ -- balance <ids>` until
the hero wins 42-58% against the classic hero(es) of its rarity and role (the RosterTests band is 40-60%).
Generated phase H heroes are tuned by newheroes.py instead.
"""
import os, re, subprocess, sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
GD = os.path.join(ROOT, "unity", "Assets", "Sigilbound", "Core", "GameData.cs")

def line_of(src, hid):
    for i, l in enumerate(src):
        if re.search(r'(H\("|Id = ")' + re.escape(hid) + '"', l): return i
    raise SystemExit("no line for " + hid)

def scale(line, f):
    def s_args(m):
        return m.group(1) + repr(round(float(m.group(2)) * f, 4))
    line = re.sub(r'(S\("[^"]*",\s*[A-Za-z.]+,\s*)([0-9.]+)', s_args, line)
    line = re.sub(r'((?:Heal|Shield)\()([0-9.]+)', s_args, line)
    return line

def rates(ids):
    out = subprocess.run(["dotnet", "run", "-c", "Release", "--project", os.path.join(ROOT, "unity", "Tests~"), "--", "balance"] + ids,
                         capture_output=True, text=True, cwd=ROOT).stdout
    return {m.group(1): float(m.group(2)) for m in re.finditer(r"RATE (\S+) ([0-9.]+)", out)}

def main(ids):
    src = open(GD).read().split("\n")
    base = {h: src[line_of(src, h)] for h in ids}
    fac = {h: 1.0 for h in ids}; lo, hi = {}, {}
    todo = list(ids)
    for rnd in range(12):
        r = rates(todo)
        print(f"round {rnd}: " + ", ".join(f"{i} {v:.0%} (x{fac[i]:.3f})" for i, v in sorted(r.items())), flush=True)
        bad = {i: v for i, v in r.items() if not 0.42 <= v <= 0.58}
        if not bad: break
        src = open(GD).read().split("\n")
        for i, v in bad.items():
            f = fac[i]
            if v < 0.5: lo[i] = max(lo.get(i, 0), f)
            else: hi[i] = min(hi.get(i, 9), f)
            f = (lo[i] + hi[i]) / 2 if i in lo and i in hi else f * (1 + (0.5 - v) * 1.2)
            fac[i] = max(0.4, min(2.0, f))
            src[line_of(src, i)] = scale(base[i], fac[i])
        open(GD, "w").write("\n".join(src))
        todo = list(bad)
    print("factors: " + ", ".join(f"{h} x{f:.3f}" for h, f in fac.items()))

if __name__ == "__main__":
    main(sys.argv[1:])
