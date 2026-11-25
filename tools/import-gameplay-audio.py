"""AU2: prepare licensed recordings in the existing audio slots, preserving Unity GUIDs.

Python standard library plus an external FFmpeg executable (conversion only).
Source archives stay in --cache, outside Assets. --verify is offline and read-only.
"""
import argparse
import array
import hashlib
import io
import json
import math
from pathlib import Path
import subprocess
import sys
import tempfile
import urllib.request
import wave
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "tools/gameplay-audio-sources.json"
DESTINATION = ROOT / "Assets/Audio/Prototype"
RATE = 22050


def digest(data):
    return hashlib.sha256(data).hexdigest()


def source_bytes(source, member, cache, download):
    path = cache / source["file"]
    if not path.exists():
        if not download:
            raise ValueError(f"Missing {path}; supply --download to fetch the recorded source.")
        path.parent.mkdir(parents=True, exist_ok=True)
        with urllib.request.urlopen(source["url"], timeout=60) as response:
            data = response.read()
        if digest(data) != source["sha256"]:
            raise ValueError("Downloaded source hash differs: " + source["url"])
        path.write_bytes(data)
    data = path.read_bytes()
    if digest(data) != source["sha256"]:
        raise ValueError("Cached source hash differs: " + str(path))
    if member and path.suffix == ".zip":
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            return archive.read(member)
    if member and path.suffix == ".7z":
        # Windows' bundled bsdtar reads this small archive; no extraction into Assets.
        return subprocess.check_output(["tar", "-xOf", str(path), member])
    if member:
        raise ValueError("Unsupported archive: " + str(path))
    return data


def decode(data, ffmpeg, filters, rate=RATE):
    command = [ffmpeg, "-v", "error", "-i", "pipe:0"]
    if filters:
        command += ["-af", filters]
    command += ["-ac", "1", "-ar", str(rate), "-f", "f32le", "pipe:1"]
    result = subprocess.run(command, input=data, capture_output=True, check=True)
    samples = array.array("f")
    samples.frombytes(result.stdout)
    if sys.byteorder != "little":
        samples.byteswap()
    return samples


def prepare(samples, recipe):
    rate = recipe.get("sample_rate", RATE)
    start = round(recipe.get("start", 0) * rate)
    samples = samples[start:]
    if not recipe.get("loop"):
        # Keep 3 ms before the attack; remove recording silence, not the transient.
        threshold = max(map(abs, samples)) * 0.01
        onset = next(i for i, value in enumerate(samples) if abs(value) > threshold)
        samples = samples[max(0, onset - round(0.003 * rate)):]
    samples = samples[:round(recipe["seconds"] * rate)]
    if recipe.get("shutdown"):
        # A falling playback rate and amplitude turn the same motor into a shutdown.
        result = array.array("f")
        phase = 0.0
        for i in range(round(recipe["seconds"] * rate)):
            t = i / (recipe["seconds"] * rate)
            index = int(phase) % len(samples)
            fraction = phase % 1
            value = samples[index] * (1 - fraction) + samples[(index + 1) % len(samples)] * fraction
            result.append(value * (1 - t) ** 1.5)
            phase += 1 - 0.85 * t
        samples = result
    if recipe.get("loop"):
        # Overlap the tail with the head; the wrap continues at the head's next sample.
        overlap = min(round(0.15 * rate), len(samples) // 4)
        body = samples[overlap:-overlap]
        for i in range(overlap):
            t = i / (overlap - 1)
            body.append(samples[-overlap + i] * (1 - t) + samples[i] * t)
        samples = body
    else:
        attack = min(round(recipe.get("attack", 0.002) * rate), len(samples) // 4)
        release = min(round(recipe.get("release", 0.035) * rate), len(samples) // 4)
        for i in range(attack):
            samples[i] *= i / attack
        for i in range(release):
            samples[-i - 1] *= i / release
    # Remove DC and set intentional per-cue headroom. No hard clipping or limiter.
    mean = sum(samples) / len(samples)
    peak = max(abs(value - mean) for value in samples)
    gain = recipe["peak"] / peak
    pcm = array.array("h", (round((value - mean) * gain * 32767) for value in samples))
    if sys.byteorder != "little":
        pcm.byteswap()
    output = io.BytesIO()
    with wave.open(output, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(rate)
        wav.writeframes(pcm.tobytes())
    return output.getvalue()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="check installed WAV hashes without decoding or writing")
    parser.add_argument("--ffmpeg", help="path to FFmpeg 7.1 executable, needed only for rebuilding")
    parser.add_argument("--cache", type=Path, default=Path(tempfile.gettempdir()) / "blackout-AU2")
    parser.add_argument("--download", action="store_true", help="fetch missing sources into the temporary cache")
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding="utf8"))
    if not args.verify and not args.ffmpeg:
        parser.error("Rebuilding needs --ffmpeg; --verify needs no converter.")
    decoded = {}
    changed = 0
    for cue, recipe in manifest["clips"].items():
        path = DESTINATION / (cue + ".wav")
        if args.verify:
            data = path.read_bytes()
        else:
            key = (recipe["source"], recipe.get("member"), recipe.get("filters", ""), recipe.get("sample_rate", RATE))
            if key not in decoded:
                raw = source_bytes(manifest["sources"][key[0]], key[1], args.cache, args.download)
                decoded[key] = decode(raw, args.ffmpeg, key[2], key[3])
            samples = decoded[key]
            data = prepare(samples, recipe)
        if digest(data) != recipe["sha256"]:
            raise ValueError("Output hash differs: " + str(path))
        if not args.verify and (not path.exists() or path.read_bytes() != data):
            # Replace content only. Never rename/delete an asset or touch its .meta.
            path.write_bytes(data)
            changed += 1
    print(f'{len(manifest["clips"])} sourced clips verified; {changed} changed; mono PCM16 at each recipe\'s sample rate.')


if __name__ == "__main__":
    main()
