"""Bake prototype/src/audio/audio.js recipes to mono PCM WAVs. Prototype is read-only.

Python standard library only. Fixed seed per cue. Filters,
exponential envelopes and oscillator recipes follow the prototype; Unity handles
spatial playback instead of WebAudio. AU2 manifest entries protect downloaded
replacements; this tool only synthesizes the remaining prototype cues.
"""
import argparse
import array
import hashlib
import io
import json
import math
from pathlib import Path
import random
import sys
import wave

RATE = 22050
ROOT = Path(__file__).resolve().parents[1]
DESTINATION = ROOT / "Assets/Audio/Prototype"


def noise(freq, duration, gain, kind="bandpass", q=1, end=None, delay=0, attack=.004, brown=False):
    return dict(noise=True, freq=freq, duration=duration, gain=gain, kind=kind,
                q=q, end=end, delay=delay, attack=attack, brown=brown)


def tone(freq, duration, gain, end=None, delay=0, shape="sine", kind=None, cutoff=800, q=1):
    return dict(noise=False, freq=freq, duration=duration, gain=gain, end=end,
                delay=delay, attack=.004, shape=shape, kind=kind, cutoff=cutoff, q=q)


# Values from audio.js: gun(), footstep(), subscribe(), buildAmbience().
RECIPES = {
    "Smg": [noise(1300,.09,.5,q=.7), noise(4000,.03,.25,"highpass"), tone(140,.1,.45,55)],
    "Sniper": [noise(4000,.55,.8,"lowpass",end=250), noise(3000,.04,.5,"highpass"), tone(90,.5,.7,35), noise(600,1.2,.12,"lowpass",delay=.25)],
    "Knife": [noise(700,.22,.18,q=2,end=3200,attack=.05)],
    "PatrolGun": [noise(950,.11,.55,q=.8), tone(120,.12,.4,50)],
    "HunterGun": [noise(3000,.35,.6,"lowpass",end=400), tone(100,.3,.45,40)],
    "GuardGun": [tone(260,.12,.25,90,shape="square",kind="lowpass",cutoff=1800), noise(1800,.08,.4,q=1.2)],
    "CommanderGun": [noise(1500,.07,.35), tone(160,.07,.25,80)],
    "StepSand": [noise(900,.16,.12,"lowpass",attack=.02)],
    "StepGrass": [noise(2500,.09,.07,"highpass",attack=.01), noise(700,.07,.06,q=.8)],
    "StepDirt": [noise(1600,.09,.12,q=.6), tone(90,.06,.08,60)],
    "StepMetal": [noise(3000,.05,.1,q=1.5), tone(920,.22,.035), tone(1370,.15,.02), tone(110,.07,.08,70)],
    "StepWood": [tone(150,.09,.14,90), noise(1200,.04,.06)],
    "StepConcrete": [noise(2200,.035,.12,q=1.2), tone(95,.07,.1,60)],
    "Jump": [noise(500,.12,.05)],
    "Land": [tone(70,.15,.3,40)],
    "Hit": [tone(1900,.05,.08)],
    "Kill": [tone(520,.18,.12,380,delay=.04,shape="triangle")],
    "PlayerHurt": [tone(90,.2,.35,50), noise(600,.15,.2,"lowpass")],
    "PlayerDeath": [tone(220,1.6,.3,40,shape="triangle")],
    "HumanHurt": [tone(170,.18,.18,110,shape="sawtooth",kind="bandpass",cutoff=750,q=2)],
    "HumanDeath": [tone(170,.55,.2,70,shape="sawtooth",kind="bandpass",cutoff=600,q=1.5), tone(60,.3,.25,40,delay=.5)],
    "RobotHurt": [noise(3500,.18,.25,q=6), tone(640,.25,.08,shape="triangle")],
    "RobotDeath": [tone(420,1.4,.25,40,shape="sawtooth",kind="lowpass",cutoff=1200)] + [noise(3000,.04,.15,"highpass",delay=i*.13) for i in range(6)],
    "Pickup": [tone(f,.18,.12,delay=i*.07) for i,f in enumerate((660,880,1320))],
    "WeaponSwitch": [noise(1400,.08,.08,q=2), noise(2600,.05,.08,q=4,delay=.18)],
    "DoorSlide": [noise(320,1,.25,"lowpass",attack=.15,brown=True), tone(70,.25,.3,50,delay=.85), noise(2200,.06,.12,q=3)],
    "DoorHeavy": [noise(320,2.2,.45,"lowpass",attack=.15,brown=True), tone(70,.25,.3,50,delay=2.05), noise(2200,.06,.12,q=3)],
    "DoorHingeOpen": [tone(260,.6,.07,340,shape="sawtooth",kind="bandpass",cutoff=900,q=6), tone(90,.12,.2,delay=.5)],
    "DoorHingeClose": [tone(260,.6,.07,200,shape="sawtooth",kind="bandpass",cutoff=900,q=6), tone(90,.12,.2,delay=.5)],
    "DoorLocked": [tone(110,.3,.08,shape="square",kind="lowpass",cutoff=600)],
    "Crate": [noise(380,.5,.2,"lowpass",attack=.05,brown=True)],
    "GeneratorStop": [tone(300,3.2,.3,30,shape="sawtooth",kind="lowpass",cutoff=900), noise(2000,2.5,.25,"lowpass",end=100)],
    "HoldTick": [tone(1500,.02,.03)],
    "Alert": [noise(1800,.35,.15,q=2)] + [tone(f,.22,.06,delay=.3+i*.25,shape="square",kind="lowpass",cutoff=2000) for i,f in enumerate((880,660,880,660))],
    "Complete": [tone(f,.6,.12,delay=i*.15,shape="triangle") for i,f in enumerate((523,659,784,1046))],
    "Insect": [tone(4600,.035,.025,delay=i*.07) for i in range(3)],
}
LOOPS = ("Waves", "Wind", "Room", "Alarm", "GeneratorHum")


def coefficients(kind, freq, q):
    w = 2*math.pi*min(freq, RATE*.45)/RATE
    c, alpha = math.cos(w), math.sin(w)/(2*q)
    if kind == "lowpass": b = ((1-c)/2, 1-c, (1-c)/2)
    elif kind == "highpass": b = ((1+c)/2, -(1+c), (1+c)/2)
    else: b = (alpha, 0, -alpha)  # constant 0 dB peak bandpass
    a0 = 1+alpha
    return tuple(v/a0 for v in (*b, -2*c, 1-alpha))


def filtered(samples, kind, freq, q=1, end=None):
    if not kind: return samples
    result = array.array('f')
    x1=x2=y1=y2=0.
    coeff = coefficients(kind, freq, q)
    count = len(samples)
    for i,x in enumerate(samples):
        if end is not None: coeff = coefficients(kind, freq*(end/freq)**(i/count), q)
        b0,b1,b2,a1,a2 = coeff
        y = b0*x+b1*x1+b2*x2-a1*y1-a2*y2
        result.append(y)
        x2,x1,y2,y1 = x1,x,y1,y
    return result


def oscillator(phase, shape):
    if shape == "square": return 1. if math.sin(phase) >= 0 else -1.
    if shape == "sawtooth": return 2*((phase/(2*math.pi))%1)-1
    if shape == "triangle": return 2/math.pi*math.asin(math.sin(phase))
    return math.sin(phase)


def white(count, rng, brown=False):
    result = array.array('f')
    last=0.
    for _ in range(count):
        w = rng.uniform(-1,1)
        if brown:
            last = (last+.02*w)/1.02
            w = last*3.5
        result.append(w)
    return result


def bake(name):
    rng = random.Random(4100 + int.from_bytes(hashlib.sha256(name.encode()).digest()[:4],'little'))
    if name in LOOPS:
        count = RATE * (5 if name == "GeneratorHum" else 8 if name in ("Wind","Waves","Room") else 5)
        if name == "GeneratorHum":
            raw = array.array('f', (sum(oscillator(2*math.pi*f*i/RATE,shape)*gain for f,shape,gain in
                ((50,"sine",.5),(100,"sawtooth",.12),(150,"sine",.15),(300,"triangle",.03))) for i in range(count)))
            samples=filtered(raw,"lowpass",420)
            samples=array.array('f',(v*(1+.08*math.sin(2*math.pi*.4*i/RATE)) for i,v in enumerate(samples)))
        elif name == "Alarm":
            phase=0.;raw=array.array('f')
            for i in range(count):
                phase+=2*math.pi*(880 if int(i/RATE*1.6)%2 == 0 else 620)/RATE
                raw.append(oscillator(phase,"square"))
            samples=filtered(raw,"bandpass",900,2)
        else:
            samples=filtered(white(count,rng,name != "Wind"),"bandpass" if name == "Wind" else "lowpass",
                             500 if name == "Wind" else 520 if name == "Waves" else 180,.7)
        # Short boundary fade prevents a discontinuity at the offline loop seam.
        fade=int(RATE*.015)
        for i in range(fade): samples[i]*=i/fade;samples[-i-1]*=i/fade
    else:
        layers=RECIPES[name]
        samples=array.array('f',[0.])*int((max(l['delay']+l['duration'] for l in layers)+.05)*RATE)
        for layer in layers:
            count=int(layer['duration']*RATE)
            if layer['noise']:
                raw=white(count,rng,layer['brown'])
                raw=filtered(raw,layer['kind'],layer['freq'],layer['q'],layer['end'])
            else:
                phase=0.;raw=array.array('f')
                for i in range(count):
                    freq=layer['freq']*((layer['end']/layer['freq'])**(i/count) if layer['end'] else 1)
                    phase+=2*math.pi*freq/RATE
                    raw.append(oscillator(phase,layer['shape']))
                raw=filtered(raw,layer['kind'],layer['cutoff'],layer['q'])
            offset=int(layer['delay']*RATE)
            for i,v in enumerate(raw):
                t=i/RATE
                env = .0001*(layer['gain']/.0001)**(t/layer['attack']) if t < layer['attack'] else layer['gain']*(.0001/layer['gain'])**((t-layer['attack'])/(layer['duration']-layer['attack']))
                samples[offset+i]+=v*env
    # Keep relative recipe levels; apply one global 0.8 master, soft-limit only over-range peaks.
    pcm=array.array('h',(round(max(-1,min(1,v*.8))*32767) for v in samples))
    if sys.byteorder != 'little': pcm.byteswap()
    output=io.BytesIO()
    with wave.open(output,'wb') as wav:
        wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(RATE);wav.writeframes(pcm.tobytes())
    return output.getvalue()


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify',action='store_true',help='compare against files without writing')
    args=parser.parse_args()
    if not args.verify: DESTINATION.mkdir(parents=True,exist_ok=True)
    manifest = ROOT / 'tools/gameplay-audio-sources.json'
    sourced = json.loads(manifest.read_text(encoding='utf8'))['clips'] if manifest.exists() else {}
    for name, recipe in sourced.items():
        path = DESTINATION / (name + '.wav')
        if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != recipe['sha256']:
            raise SystemExit('Missing or changed sourced audio: '+str(path)+'; rebuild with tools/import-gameplay-audio.py.')
    changed=0
    for name in (*RECIPES,*LOOPS):
        path=DESTINATION/(name+'.wav')
        if name in sourced:
            continue
        data=bake(name)
        same=path.exists() and path.read_bytes()==data
        if not same:
            changed+=1
            if args.verify: raise SystemExit('Non-deterministic or missing audio: '+str(path))
            path.write_bytes(data)
    generated = sum(name not in sourced for name in (*RECIPES, *LOOPS))
    print(f'{generated} prototype clips at {RATE} Hz; {len(sourced)} protected sourced clips; {changed} changed; sourced formats retained.')


if __name__ == '__main__': main()
