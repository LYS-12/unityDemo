"""Deterministic original short game sounds, 44.1 kHz / 16-bit PCM.
No samples or game recordings are used. Each sound is a small additive/FM and
filtered-noise instrument; the seed makes the delivered assets reproducible.
"""
from pathlib import Path
import math, random, struct, wave

root = Path(__file__).resolve().parents[2]
folder = root / 'Assets/_Project/Audio/SFX/ForestParty'
folder.mkdir(parents=True, exist_ok=True)
rate = 44100
random.seed(23)
specs = {
 'Hover':(.09,780,.15), 'Confirm':(.28,660,.12), 'Back':(.2,480,.1),
 'Pickup':(.33,880,.15), 'Equip':(.2,560,.14), 'Use':(.38,720,.18),
 'Throw':(.3,280,.55), 'Hit':(.16,170,.45), 'Hurt':(.27,240,.28),
 'Shield':(.45,1040,.15), 'Explosion':(.65,80,.85),
 'FoxStartle':(.45,620,.15), 'Rescue':(.75,660,.1), 'Complete':(1.3,520,.08),
 'TalkGhost':(.065,580,.08), 'TalkFox':(.065,840,.1)
}
for name,(duration,freq,noise) in specs.items():
    samples=[]; smooth=0; phase=0
    for i in range(int(rate*duration)):
        t=i/rate; u=t/duration
        if name in ('Confirm','Pickup','Rescue','Complete'): step=int(u*(4 if name=='Complete' else 3)); f=freq*[1,1.25,1.5,2][min(3,step)]
        elif name in ('Throw','Explosion','Hit','Hurt'): f=freq*(1.3-.9*u)
        elif name=='FoxStartle': f=freq*(1+.6*math.sin(u*math.pi))
        else: f=freq*(1+.10*math.sin(u*math.pi))
        phase+=math.tau*f/rate
        smooth=smooth*.7+random.uniform(-1,1)*.3
        tone=math.sin(phase+1.2*math.sin(phase*.5)*math.exp(-u*7))*.6+math.sin(phase*2.01)*.14
        env=min(1,t/.008)*math.exp(-u*(3 if name in ('Explosion','Complete','Rescue') else 5))*(1-u)**.4
        value=(tone*(1-noise)+smooth*noise*2)*env*.65
        samples.append(struct.pack('<h',int(max(-1,min(1,value))*32767)))
    with wave.open(str(folder/(name+'.wav')),'wb') as output:
        output.setnchannels(1); output.setsampwidth(2); output.setframerate(rate); output.writeframes(b''.join(samples))
(folder/'README.txt').write_text('Original synthesized sound effects for Forest Party.\nCreated by Tools/Art/create_party_audio.py. No third-party recordings.\n',encoding='utf-8')
print(f'Created {len(specs)} original effects')
