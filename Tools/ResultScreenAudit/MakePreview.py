"""Combine Unity-rendered IntroFrames into a small animation for review."""
from pathlib import Path
import sys
from PIL import Image

output = Path(sys.argv[1])
paths = sorted((output / "IntroFrames").glob("Frame*.png"))
if len(paths) != 41:
    raise SystemExit("Expected 41 Unity-rendered frames (0–2 seconds at 20 FPS).")
frames = []
for path in paths:
    with Image.open(path) as source:
        frames.append(source.convert("RGB").resize((540, 960), Image.Resampling.LANCZOS))
# Use the separately verified completed state for the hold, avoiding a floating-point
# clock landing just below 2.0 seconds with the skip overlay still enabled.
with Image.open(output / "CrewVictory.png") as source:
    frames[-1] = source.convert("RGB").resize((540, 960), Image.Resampling.LANCZOS)
durations = [50] * len(frames)
durations[-1] = 2000
target = output / "ResultIntro.gif"
frames[0].save(target, save_all=True, append_images=frames[1:], duration=durations, loop=0)
print(target)
