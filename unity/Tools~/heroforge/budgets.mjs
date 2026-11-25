// Reads the budgets from Core/Art/ArtSpec.cs so the Node tools never drift from the C# rules.
import fs from "node:fs";
import path from "node:path";
import { ROOT } from "./prompts.mjs";

export function budgets() {
  const src = fs.readFileSync(path.join(ROOT, "unity/Assets/Sigilbound/Core/Art/ArtSpec.cs"), "utf8");
  const out = {};
  for (const m of src.matchAll(/public const (?:int|double) (\w+) = ([\d.]+)/g)) out[m[1]] = Number(m[2]);
  if (!out.TrisLod1 || !out.TrisLod2) throw new Error("budgets not found in ArtSpec.cs");
  return out;
}
