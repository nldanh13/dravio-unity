"""Procedural ambience for the Ocean and Sky kits (U1-ENV.4).

Same rules as ambience_audio.py: everything is synthesised with numpy from noise and
oscillators (no samples, no recordings, no third-party audio), 22.05 kHz mono 16-bit,
loops seamless by construction (frequency-domain noise + circular event placement).

    python scripts/art/environment/zone_audio.py <out_dir> ocean|sky [--demo demo.wav]
"""
import math
import os
import sys

import numpy as np

import ambience_audio as aa

SR = aa.SR
SEED = {"ocean": 9101, "sky": 9301}

LOOPS = {
    "ocean": ["AMB_Ocean_Underwater_Bed_Loop", "AMB_Ocean_Current_Loop", "AMB_Ocean_Reef_Crackle_Loop",
              "AMB_Ocean_Deep_Song_Loop", "AMB_Ocean_Vent_Bubbles_Loop", "AMB_Ocean_Estuary_Flow_Loop"],
    "sky": ["AMB_Sky_High_Wind_Loop", "AMB_Sky_Birds_Loop", "AMB_Sky_Night_Shimmer_Loop", "AMB_Sky_Chimes_Loop"],
}
ONE_SHOTS = {
    "ocean": ["SFX_Ocean_Sand_Puff", "SFX_Ocean_Coral_Tick", "SFX_Ocean_Rune_Chime", "SFX_Ocean_Glow_Shimmer",
              "SFX_Ocean_Bubbles"],
    "sky": ["SFX_Sky_Petal_Rustle", "SFX_Sky_Wind_Chime", "SFX_Sky_Cloud_Puff", "SFX_Sky_Wing_Gust"],
}
PENTATONIC = [523.25, 587.33, 659.25, 783.99, 880.0, 1046.5, 1174.66]


def bubble(rng, f0=None, d=None, amp=1.0):
    d = d or rng.uniform(0.02, 0.06)
    m = int(d * SR)
    tt = np.arange(m) / SR
    f0 = f0 or rng.uniform(380, 900)
    f = f0 * (1 + 1.4 * tt / d)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / (d * 0.35)) * amp


def bell(rng, f0, dur=1.6, bright=1.0):
    n = int(dur * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for ratio, amp, decay in ((1.0, 1.0, 0.8), (2.76, 0.45 * bright, 0.4), (5.4, 0.2 * bright, 0.22)):
        out += amp * np.sin(2 * np.pi * f0 * ratio * t + rng.uniform()) * np.exp(-t / (decay * dur / 1.6))
    return out * aa.env(n, 0.002, 0.25)


# ------------------------------------------------------------------- ocean
def ocean_bed(rng, n):
    swell = aa.periodic_lfo(n, rng, (1, 2, 3), depth=0.55)
    deep = aa.shaped_noise(n, rng, 35, 380, slope=-1.8) * swell
    air = aa.shaped_noise(n, rng, 400, 1400, slope=-1.0) * 0.06 * swell ** 2
    return aa.limit(aa.to_rms(deep + air, -20.0), -3.0)


def ocean_current(rng, n):
    flow = aa.periodic_lfo(n, rng, (1, 2, 4), depth=0.7)
    x = aa.shaped_noise(n, rng, 120, 900, slope=-1.2) * flow
    return aa.limit(aa.to_rms(x, -23.0), -3.0)


def ocean_crackle(rng, n):
    out = np.zeros(n)
    dens = aa.periodic_lfo(n, rng, (1, 3, 5), depth=0.6)
    for _ in range(int(n / SR * 90)):
        pos = int(rng.uniform(0, n))
        if rng.random() > dens[pos]:
            continue
        m = int(rng.uniform(0.0015, 0.004) * SR)
        click = rng.normal(size=m) * np.exp(-np.arange(m) / (m * 0.25))
        aa.add_circular(out, click * rng.uniform(0.2, 1.0), pos)
    out = aa.band(out, 1800, 7000)
    return aa.limit(aa.to_rms(out, -27.0), -4.0)


def ocean_song(rng, n):
    out = aa.shaped_noise(n, rng, 40, 160, slope=-2.0) * 0.25
    t = 1.5
    while t < n / SR - 1.0:
        dur = rng.uniform(2.2, 4.0)
        f0 = rng.uniform(110, 190)
        s = aa.sweep(f0, f0 * rng.uniform(1.3, 1.8), dur, harm=0.35, shape=rng.uniform(0.6, 1.6))
        s += 0.3 * aa.sweep(f0 * 2, f0 * 2.5, dur, harm=0.1)
        aa.add_circular(out, aa.band(np.pad(s, 600), 80, 900)[600:-600] * 0.6, int(t * SR))
        t += dur + rng.uniform(2.5, 5.0)
    return aa.limit(aa.to_rms(out, -24.0), -3.0)


def ocean_vent(rng, n):
    out = np.zeros(n)
    t = 0.0
    while t < n / SR:  # bursts of rising bubbles
        k = int(rng.integers(4, 12))
        for j in range(k):
            aa.add_circular(out, bubble(rng, rng.uniform(450, 1100), amp=rng.uniform(0.1, 0.3)),
                            int((t + j * rng.uniform(0.03, 0.09)) * SR))
        t += rng.uniform(0.25, 0.9)
    hiss = aa.shaped_noise(n, rng, 300, 1600, slope=-1.0) * 0.03
    return aa.limit(aa.to_rms(out, -24.0) + hiss, -3.0)


def ocean_estuary(rng, n):
    mod = aa.periodic_lfo(n, rng, (2, 3, 5), depth=0.5)
    flow = aa.shaped_noise(n, rng, 220, 2200, slope=-0.9) * mod
    trickle = aa.shaped_noise(n, rng, 1400, 3800, slope=0) * 0.05 * mod ** 3
    return aa.limit(aa.to_rms(aa.band(flow, 150, 1800) + trickle, -24.0), -3.0)


# --------------------------------------------------------------------- sky
def sky_wind(rng, n):
    gust = aa.periodic_lfo(n, rng, (1, 2, 3, 5), depth=0.8)
    body = aa.shaped_noise(n, rng, 180, 2200, slope=-1.2) * gust
    whistle = aa.shaped_noise(n, rng, 900, 1300, slope=0, edge=0.06) * 0.1 * gust ** 2
    high = aa.shaped_noise(n, rng, 2500, 8000, slope=0) * 0.08 * gust ** 3
    return aa.limit(aa.to_rms(body + whistle + high, -21.0), -3.0)


def sky_birds(rng, n):
    out = np.zeros(n)
    t = 0.5
    while t < n / SR:
        s = aa.bird_song(rng) * rng.uniform(0.25, 0.7)
        aa.add_circular(out, s, int(t * SR))
        t += rng.uniform(1.2, 3.4)
    for _ in range(5):  # distant soaring calls
        f0 = rng.uniform(1800, 2400)
        s = aa.sweep(f0, f0 * 0.7, rng.uniform(0.5, 0.8), harm=0.2, shape=0.8) * 0.25
        aa.add_circular(out, s, int(rng.uniform(0, n)))
    air = aa.shaped_noise(n, rng, 200, 3000, slope=-1.0) * 0.04
    return aa.limit(aa.to_rms(out, -24.0) + air, -3.0)


def sky_shimmer(rng, n):
    t = np.arange(n) / SR
    out = np.zeros(n)
    L = n / SR
    for f, cyc in ((392.0, 3), (587.33, 5), (783.99, 7), (1174.66, 4)):
        f = round(f * L) / L  # whole cycles per loop -> seamless
        lfo = 0.5 + 0.5 * np.sin(2 * np.pi * cyc * t / (n / SR) + rng.uniform(0, 6.28))
        out += np.sin(2 * np.pi * f * t) * lfo * 0.25 + np.sin(2 * np.pi * (f + 12 / L) * t) * lfo * 0.15
    out += aa.shaped_noise(n, rng, 2000, 9000, slope=0) * 0.05
    return aa.limit(aa.to_rms(out, -28.0), -4.0)


def sky_chimes(rng, n):
    out = np.zeros(n)
    gust = aa.periodic_lfo(n, rng, (1, 2, 3), depth=0.8)
    t = 0.2
    while t < n / SR:
        pos = int(t * SR) % n
        if rng.random() < 0.25 + 0.75 * gust[pos]:
            b = bell(rng, PENTATONIC[int(rng.integers(len(PENTATONIC)))], dur=rng.uniform(1.2, 2.2))
            aa.add_circular(out, b * rng.uniform(0.15, 0.5), pos)
        t += rng.uniform(0.18, 0.7)
    return aa.limit(aa.to_rms(out, -26.0), -3.0)


LOOP_BUILDERS = {
    "AMB_Ocean_Underwater_Bed_Loop": ocean_bed, "AMB_Ocean_Current_Loop": ocean_current,
    "AMB_Ocean_Reef_Crackle_Loop": ocean_crackle, "AMB_Ocean_Deep_Song_Loop": ocean_song,
    "AMB_Ocean_Vent_Bubbles_Loop": ocean_vent, "AMB_Ocean_Estuary_Flow_Loop": ocean_estuary,
    "AMB_Sky_High_Wind_Loop": sky_wind, "AMB_Sky_Birds_Loop": sky_birds,
    "AMB_Sky_Night_Shimmer_Loop": sky_shimmer, "AMB_Sky_Chimes_Loop": sky_chimes,
}


# --------------------------------------------------------------- one-shots
def one_shot(name, rng):
    if name == "SFX_Ocean_Sand_Puff":
        x = aa.noise_burst(rng, 0.5, 80, 700) * np.exp(-np.arange(int(0.5 * SR)) / (0.12 * SR))
    elif name == "SFX_Ocean_Coral_Tick":
        x = np.zeros(int(0.7 * SR))
        for _ in range(6):
            m = int(0.003 * SR)
            s = int(rng.uniform(0, 0.3) * SR)
            x[s:s + m] += rng.normal(size=m) * rng.uniform(0.3, 1.0)
        x = aa.band(x, 1500, 6000)
        b = bubble(rng, 700, 0.05, 0.6)
        x[int(0.35 * SR):int(0.35 * SR) + len(b)] += b
    elif name == "SFX_Ocean_Rune_Chime":
        x = aa.band(np.pad(aa.sfx_chime(rng), 2000), 200, 2500)[2000:-2000]
    elif name == "SFX_Ocean_Glow_Shimmer":
        n = int(1.6 * SR)
        t = np.arange(n) / SR
        x = sum(np.sin(2 * np.pi * f * t) * (0.6 + 0.4 * np.sin(2 * np.pi * (5 + i) * t))
                for i, f in enumerate((1318.5, 1567.98, 1975.53)))
        x = x * aa.env(n, 0.25, 0.9)
    elif name == "SFX_Ocean_Bubbles":
        x = np.zeros(int(1.0 * SR))
        for j in range(14):
            b = bubble(rng, 400 + j * 45, amp=rng.uniform(0.3, 0.8))
            s = int((j * 0.05 + rng.uniform(0, 0.03)) * SR)
            x[s:s + len(b)] += b[:len(x) - s]
    elif name == "SFX_Sky_Petal_Rustle":
        x = aa.sfx_rustle(rng, 0.55, 2500, 8000, 2)
    elif name == "SFX_Sky_Wind_Chime":
        x = np.zeros(int(2.2 * SR))
        for j, f in enumerate((783.99, 1046.5, 1174.66)):
            b = bell(rng, f, 1.8)
            s = int(j * 0.11 * SR)
            x[s:s + len(b)] += b[:len(x) - s] * (1 - j * 0.2)
    elif name == "SFX_Sky_Cloud_Puff":
        m = int(0.8 * SR)
        x = aa.noise_burst(rng, 0.8, 200, 2500, attack=0.08, release=0.6) * np.hanning(m)
    elif name == "SFX_Sky_Wing_Gust":
        m = int(0.9 * SR)
        noise = rng.normal(size=m)
        x = np.zeros(m)
        for k, (lo, hi) in enumerate(((200, 800), (400, 1500), (800, 2800))):
            seg = aa.band(np.pad(noise, 800), lo, hi)[800:-800]
            x += seg * np.exp(-((np.arange(m) / m - (0.3 + 0.15 * k)) / 0.16) ** 2)
    else:
        raise KeyError(name)
    return aa.limit(x / (np.max(np.abs(x)) + 1e-9) * 0.7, -3.0)


def generate_zone(zone, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    n = int(aa.LOOP_SECONDS * SR)
    clips = {}
    for i, name in enumerate(LOOPS[zone]):
        x = LOOP_BUILDERS[name](np.random.default_rng(SEED[zone] + i * 101), n)
        clips[name] = x
        aa.write_wav(os.path.join(out_dir, name + ".wav"), x)
    for i, name in enumerate(ONE_SHOTS[zone]):
        x = one_shot(name, np.random.default_rng(SEED[zone] + 5000 + i * 37))
        clips[name] = x
        aa.write_wav(os.path.join(out_dir, name + ".wav"), x)
    return clips


def demo_mix(zone_mod, clips, path, seconds_per_phase=10.0):
    """Day -> dusk -> night -> dawn listening demo with the zone's living mix weights."""
    import zone_common as zc
    hours = [13.0, 18.6, 22.0, 6.0]
    seg = int(seconds_per_phase * SR)
    out = np.zeros(seg * len(hours))
    for loop_id, (clip, key, emitter) in zone_mod.AUDIO_LOOPS.items():
        w = np.zeros(len(out))
        for j, h in enumerate(hours):
            w[j * seg:(j + 1) * seg] = zc.evaluate(zone_mod, h)[key] * (0.55 if emitter else 1.0)
        k = int(1.5 * SR)
        c = np.cumsum(np.pad(w, (k, k), mode="edge"))
        w = (c[2 * k:] - c[:-2 * k])[:len(out)] / (2 * k)
        out += np.tile(clips[clip], len(out) // len(clips[clip]) + 1)[:len(out)] * w * 0.6
    shots = ONE_SHOTS[zone_mod.ZONE_ID]
    for i, name in enumerate(shots):
        c = clips[name] * 0.5
        s = int((3.0 + i * 7.0) * SR)
        out[s:s + len(c)] += c[:len(out) - s]
    aa.write_wav(path, aa.limit(out, -1.0))
    return path


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "Audio"
    zone = sys.argv[2] if len(sys.argv) > 2 else "ocean"
    c = generate_zone(zone, out)
    if "--demo" in sys.argv:
        demo_mix(__import__(f"{zone}_zone"), c, sys.argv[sys.argv.index("--demo") + 1])
    print("wrote", len(c), "clips ->", out)
