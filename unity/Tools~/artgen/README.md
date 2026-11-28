# artgen: Gemini concept images -> fal.ai TRELLIS 3D models

See `.claude/skills/art-pipeline/SKILL.md` for the full workflow and prompt rules.
- `gemini_image.py`: image generation (key injected by the environment's network proxy).
- `cutout.py`: plain background -> transparent PNG (avoids TRELLIS backdrop cards).
- `fal_trellis.py`: image -> .glb via fal.ai `fal-ai/trellis` (key injected by the proxy, or FAL_KEY / .env locally).
- `sheet.py`: side-by-side contact sheet for owner review.
