"""Procedural ambience for the Origin Sanctuary (U1-ENV.3).

Every sound is synthesised here from noise and oscillators with numpy. No samples,
recordings or third-party libraries are used, so the output is original project
material with no licence obligations.

    python scripts/art/environment/ambience_audio.py <out_dir> [--demo demo.wav]

Writes 22.05 kHz mono 16-bit WAV files. Loops are built in the frequency domain and
events are placed circularly, so every AMB_*_Loop is seamless by construction.
Unity re-encodes them (Vorbis for loops, ADPCM for one-shots) on import.
"""
import math
import os
import sys
import wave

import numpy as np

SR = 22050
LOOP_SECONDS = 20.0
SEED = 7341

LOOPS = ["AMB_Day_Meadow_Loop", "AMB_Night_Crickets_Loop", "AMB_Wind_Loop", "AMB_Water_Pond_Loop",
         "AMB_Night_Frogs_Loop", "AMB_Warm_Rocks_Insects_Loop"]
ONE_SHOTS = ["SFX_Splash_Small", "SFX_Drink_Lap", "SFX_Rune_Chime", "SFX_Flower_Rustle", "SFX_Leaves_Rustle",
             "SFX_Rock_Warm_Settle"]


# ------------------------------------------------------------------ helpers
def shaped_noise(n, rng, lo, hi, slope=-1.0, edge=0.35):
    """Periodic band-limited noise. slope -1 = pink, -2 = brown. Soft log-frequency edges."""
    spec = rng.normal(size=n // 2 + 1) + 1j * rng.normal(size=n // 2 + 1)
    f = np.fft.rfftfreq(n, 1.0 / SR)
    f[0] = 1.0
    lf = np.log2(f)
    mask = np.exp(-np.clip(math.log2(lo) - lf, 0, None) ** 2 / (2 * edge ** 2))
    mask *= np.exp(-np.clip(lf - math.log2(hi), 0, None) ** 2 / (2 * edge ** 2))
    mask *= f ** (slope / 2.0)
    mask[0] = 0.0
    x = np.fft.irfft(spec * mask, n)
    return x / (np.max(np.abs(x)) + 1e-9)


def band(x, lo, hi, edge=0.3):
    """Zero-phase band filter of a finite signal (circular, so pad one-shots)."""
    n = len(x)
    f = np.fft.rfftfreq(n, 1.0 / SR)
    f[0] = 1.0
    lf = np.log2(f)
    mask = np.exp(-np.clip(math.log2(lo) - lf, 0, None) ** 2 / (2 * edge ** 2))
    mask *= np.exp(-np.clip(lf - math.log2(hi), 0, None) ** 2 / (2 * edge ** 2))
    mask[0] = 0.0
    return np.fft.irfft(np.fft.rfft(x) * mask, n)


def periodic_lfo(n, rng, cycles=(1, 2, 3, 5), depth=1.0):
    """Smooth 0..1 modulation with whole cycles per loop (seamless)."""
    t = np.arange(n) / n
    y = np.zeros(n)
    for c in cycles:
        y += rng.uniform(0.4, 1.0) / c * np.sin(2 * np.pi * (c * t + rng.uniform()))
    y = (y - y.min()) / (y.max() - y.min() + 1e-9)
    return 1.0 - depth + depth * y


def add_circular(buf, ev, start):
    n = len(buf)
    start %= n
    end = start + len(ev)
    if end <= n:
        buf[start:end] += ev
    else:
        k = n - start
        buf[start:] += ev[:k]
        buf[:end - n] += ev[k:]


def env(n, attack=0.01, release=0.2, curve=2.0):
    a = max(1, int(attack * SR))
    r = max(1, int(release * SR))
    e = np.ones(n)
    a = min(a, n)
    e[:a] = np.linspace(0, 1, a)
    r = min(r, n - a) if n - a > 0 else 1
    e[n - r:] *= np.linspace(1, 0, r) ** curve
    return e


def sweep(f0, f1, dur, harm=0.25, shape=1.0):
    n = int(dur * SR)
    t = np.linspace(0, 1, n)
    f = f0 + (f1 - f0) * t ** shape
    ph = 2 * np.pi * np.cumsum(f) / SR
    return (np.sin(ph) + harm * np.sin(2 * ph)) * np.hanning(n)


def rms(x):
    return float(np.sqrt(np.mean(x ** 2)) + 1e-12)


def to_rms(x, db):
    return x * (10 ** (db / 20.0) / rms(x))


def limit(x, peak_db=-1.0):
    p = np.max(np.abs(x))
    lim = 10 ** (peak_db / 20.0)
    return x * (lim / p) if p > lim else x


# -------------------------------------------------------------------- loops
def bird_song(rng):
    kind = rng.integers(0, 4)
    if kind == 0:   # falling "tee-oo"
        f0 = rng.uniform(3600, 4600)
        s = np.concatenate([sweep(f0, f0 * 0.68, 0.16, shape=0.7), np.zeros(int(0.05 * SR)),
                            sweep(f0 * 0.9, f0 * 0.62, 0.2, shape=0.8)])
    elif kind == 1:  # trill
        f0 = rng.uniform(4200, 5600)
        parts = []
        for _ in range(rng.integers(5, 10)):
            parts += [sweep(f0, f0 * 1.15, 0.035), np.zeros(int(0.028 * SR))]
        s = np.concatenate(parts)
    elif kind == 2:  # rising "twit twit"
        f0 = rng.uniform(2600, 3400)
        parts = []
        for _ in range(rng.integers(2, 4)):
            parts += [sweep(f0, f0 * 1.6, 0.07, shape=1.5), np.zeros(int(0.11 * SR))]
        s = np.concatenate(parts)
    else:            # soft warble
        n = int(rng.uniform(0.35, 0.6) * SR)
        t = np.arange(n) / SR
        f = rng.uniform(2400, 3000) + 380 * np.sin(2 * np.pi * rng.uniform(9, 14) * t)
        s = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.hanning(n)
    return s


def loop_day_meadow(rng, n):
    air = shaped_noise(n, rng, 120, 1100) * 0.05 * periodic_lfo(n, rng, depth=0.5)
    leaves = shaped_noise(n, rng, 2000, 6500, slope=0) * 0.012 * periodic_lfo(n, rng, (2, 3, 7), depth=0.8)
    birds = np.zeros(n)
    for _ in range(16):
        s = bird_song(rng)
        dist = rng.uniform(0.25, 1.0)
        if dist < 0.5:
            s = band(np.pad(s, 400), 1500, 4200)[400:-400]
        add_circular(birds, s * dist * 0.5, int(rng.uniform(0, n)))
    return limit(air + leaves + birds, -3.0)


def loop_night_crickets(rng, n):
    out = shaped_noise(n, rng, 90, 700) * 0.035 * periodic_lfo(n, rng, depth=0.4)
    for _ in range(6):
        fc = rng.uniform(3900, 5100)
        amp = rng.uniform(0.08, 0.22)
        period = rng.uniform(0.45, 0.9)
        pulses = rng.integers(2, 5)
        on, off = rng.uniform(0.012, 0.02), rng.uniform(0.01, 0.016)
        one = []
        for _ in range(pulses):
            m = int(on * SR)
            t = np.arange(m) / SR
            one += [np.sin(2 * np.pi * fc * t) * np.hanning(m), np.zeros(int(off * SR))]
        chirp = np.concatenate(one)
        t0 = rng.uniform(0, period)
        k = 0
        while t0 + k * period < n / SR:
            if rng.uniform() > 0.08:  # small irregular gaps
                add_circular(out, chirp * amp * rng.uniform(0.85, 1.0), int((t0 + k * period) * SR))
            k += 1
    return limit(out, -3.0)


def loop_wind(rng, n):
    gust = periodic_lfo(n, rng, (1, 2, 3, 4), depth=0.75)
    body = shaped_noise(n, rng, 140, 1500, slope=-1.4) * gust
    whistle = shaped_noise(n, rng, 600, 900, slope=0, edge=0.08) * 0.08 * gust ** 2
    rustle = shaped_noise(n, rng, 1800, 6500, slope=0) * 0.18 * gust ** 3
    return limit(to_rms(body + whistle + rustle, -22.0), -3.0)


def loop_water(rng, n):
    lap = np.zeros(n)
    t = 0.0
    while t < n / SR:
        dur = rng.uniform(0.5, 1.1)
        m = int(dur * SR)
        e = np.sin(np.linspace(0, np.pi, m)) ** 2 * rng.uniform(0.4, 1.0)
        add_circular(lap, e, int(t * SR))
        t += rng.uniform(0.9, 2.2)
    base = shaped_noise(n, rng, 70, 650) * (0.25 + 0.75 * lap / (lap.max() + 1e-9))
    trickle = shaped_noise(n, rng, 1500, 4000, slope=0) * 0.04
    bubbles = np.zeros(n)
    for _ in range(28):
        d = rng.uniform(0.025, 0.06)
        m = int(d * SR)
        tt = np.arange(m) / SR
        f0 = rng.uniform(380, 700)
        f = f0 * (1 + 1.4 * tt / d)
        b = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / (d * 0.35))
        add_circular(bubbles, b * rng.uniform(0.05, 0.16), int(rng.uniform(0, n)))
    return limit(to_rms(base, -21.0) + trickle * 0.3 + bubbles, -3.0)


def croak(rng):
    dur = rng.uniform(0.18, 0.32)
    m = int(dur * SR)
    t = np.arange(m) / SR
    f0 = rng.uniform(110, 170)
    saw = 2 * ((f0 * t) % 1.0) - 1
    am = 0.5 + 0.5 * np.sign(np.sin(2 * np.pi * rng.uniform(22, 32) * t))
    s = saw * am * np.hanning(m)
    s = np.pad(s, 600)
    s = band(s, 350, 1600, edge=0.25)
    return s / (np.max(np.abs(s)) + 1e-9)


def loop_frogs(rng, n):
    out = shaped_noise(n, rng, 80, 500) * 0.02
    for _ in range(11):
        c = croak(rng) * rng.uniform(0.25, 0.6)
        t = int(rng.uniform(0, n))
        add_circular(out, c, t)
        if rng.uniform() < 0.45:  # "rib-bit"
            add_circular(out, croak(rng) * rng.uniform(0.2, 0.45), t + int(rng.uniform(0.28, 0.4) * SR))
    return limit(out, -3.0)


def loop_insects(rng, n):
    t = np.arange(n) / SR
    am = 0.55 + 0.45 * np.sin(2 * np.pi * 90.0 * t)  # 90 Hz = 1800 whole cycles in 20 s
    buzz = shaped_noise(n, rng, 4800, 7200, slope=0, edge=0.15) * am * periodic_lfo(n, rng, (1, 2, 3), depth=0.7)
    out = to_rms(buzz, -30.0)
    for _ in range(3):  # passing bee
        d = rng.uniform(1.2, 2.2)
        m = int(d * SR)
        tt = np.arange(m) / SR
        f = rng.uniform(190, 240) + 12 * np.sin(2 * np.pi * 6 * tt)
        ph = 2 * np.pi * np.cumsum(f) / SR
        bee = (np.sin(ph) + 0.5 * np.sin(2 * ph) + 0.25 * np.sin(3 * ph)) * np.hanning(m) ** 1.5
        add_circular(out, bee * 0.035, int(rng.uniform(0, n)))
    return limit(out, -3.0)


LOOP_BUILDERS = {
    "AMB_Day_Meadow_Loop": loop_day_meadow,
    "AMB_Night_Crickets_Loop": loop_night_crickets,
    "AMB_Wind_Loop": loop_wind,
    "AMB_Water_Pond_Loop": loop_water,
    "AMB_Night_Frogs_Loop": loop_frogs,
    "AMB_Warm_Rocks_Insects_Loop": loop_insects,
}


# ---------------------------------------------------------------- one-shots
def noise_burst(rng, dur, lo, hi, attack=0.005, release=None):
    m = int(dur * SR)
    x = band(np.pad(rng.normal(size=m), 800), lo, hi)[800:-800]
    return x * env(m, attack, release if release is not None else dur * 0.9)


def sfx_splash(rng):
    n = int(0.9 * SR)
    out = np.zeros(n)
    out[:int(0.35 * SR)] += noise_burst(rng, 0.35, 300, 4500) * np.exp(-np.arange(int(0.35 * SR)) / (0.08 * SR))
    for _ in range(9):
        d = rng.uniform(0.02, 0.05)
        m = int(d * SR)
        tt = np.arange(m) / SR
        f = rng.uniform(800, 1600) * (1 + 1.2 * tt / d)
        b = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / (d * 0.3))
        s = int(rng.uniform(0.06, 0.6) * SR)
        out[s:s + m] += b * rng.uniform(0.15, 0.4)
    return out


def sfx_drink(rng):
    n = int(1.0 * SR)
    out = np.zeros(n)
    for i in range(3):
        s = int((0.05 + i * 0.28) * SR)
        b = noise_burst(rng, 0.16, 200, 1300) * np.hanning(int(0.16 * SR))
        out[s:s + len(b)] += b * 0.7
        d = 0.045
        m = int(d * SR)
        tt = np.arange(m) / SR
        bl = np.sin(2 * np.pi * np.cumsum(rng.uniform(450, 600) * (1 + tt / d)) / SR) * np.exp(-tt / 0.015)
        out[s + int(0.06 * SR):s + int(0.06 * SR) + m] += bl * 0.3
    return out


def sfx_chime(rng):
    n = int(1.8 * SR)
    t = np.arange(n) / SR
    f0 = 880.0
    out = np.zeros(n)
    for ratio, amp, decay in ((1.0, 1.0, 0.9), (2.76, 0.5, 0.45), (5.4, 0.25, 0.25), (8.93, 0.12, 0.15)):
        out += amp * np.sin(2 * np.pi * f0 * ratio * t + rng.uniform()) * np.exp(-t / decay)
    second = np.zeros(n)
    s = int(0.12 * SR)
    for ratio, amp, decay in ((1.5, 0.6, 0.7), (4.1, 0.2, 0.25)):
        second[s:] += amp * np.sin(2 * np.pi * f0 * ratio * t[:n - s]) * np.exp(-t[:n - s] / decay)
    return (out + second) * env(n, 0.002, 0.3)


def sfx_rustle(rng, dur, lo, hi, bumps):
    m = int(dur * SR)
    x = noise_burst(rng, dur, lo, hi, release=0.0)
    e = np.zeros(m)
    for _ in range(bumps):
        c = rng.uniform(0.1, 0.8) * m
        w = rng.uniform(0.04, 0.12) * SR
        e += np.exp(-((np.arange(m) - c) / w) ** 2) * rng.uniform(0.5, 1.0)
    return x * e * env(m, 0.01, 0.1)


def sfx_rock(rng):
    n = int(0.9 * SR)
    t = np.arange(n) / SR
    thunk = np.sin(2 * np.pi * 92 * t) * np.exp(-t / 0.07)
    thunk[:int(0.2 * SR)] += noise_burst(rng, 0.2, 100, 600) * np.exp(-t[:int(0.2 * SR)] / 0.05) * 0.6
    for _ in range(10):
        s = int(rng.uniform(0.15, 0.8) * SR)
        m = int(0.004 * SR)
        thunk[s:s + m] += rng.normal(size=m) * rng.uniform(0.05, 0.15)
    return thunk


def one_shot(name, rng):
    if name == "SFX_Splash_Small":
        x = sfx_splash(rng)
    elif name == "SFX_Drink_Lap":
        x = sfx_drink(rng)
    elif name == "SFX_Rune_Chime":
        x = sfx_chime(rng)
    elif name == "SFX_Flower_Rustle":
        x = sfx_rustle(rng, 0.6, 1500, 6500, 2)
    elif name == "SFX_Leaves_Rustle":
        x = sfx_rustle(rng, 0.85, 800, 5000, 3)
    elif name == "SFX_Rock_Warm_Settle":
        x = sfx_rock(rng)
    else:
        raise KeyError(name)
    return limit(x / (np.max(np.abs(x)) + 1e-9) * 0.7, -3.0)


# ---------------------------------------------------------------------- io
def write_wav(path, x):
    x = np.clip(x, -1.0, 1.0)
    pcm = (x * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def read_wav(path):
    with wave.open(path, "rb") as w:
        data = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32767.0
        return data, w.getframerate(), w.getnchannels()


def generate_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    n = int(LOOP_SECONDS * SR)
    clips = {}
    for i, name in enumerate(LOOPS):
        x = LOOP_BUILDERS[name](np.random.default_rng(SEED + i * 101), n)
        clips[name] = x
        write_wav(os.path.join(out_dir, name + ".wav"), x)
    for i, name in enumerate(ONE_SHOTS):
        x = one_shot(name, np.random.default_rng(SEED + 5000 + i * 37))
        clips[name] = x
        write_wav(os.path.join(out_dir, name + ".wav"), x)
    return clips


def demo_mix(clips, path, seconds_per_phase=12.0):
    """Day -> dusk -> night -> dawn listening demo using the living.py mix weights."""
    import living
    hours = [13.0, 18.6, 22.0, 6.0]
    seg = int(seconds_per_phase * SR)
    out = np.zeros(seg * len(hours))
    loops = {lp["weightKey"]: clips[lp["clip"]] for lp in living.build_living()["audioLoops"]}
    focus = {"audioPond": 0.55, "audioFrogs": 0.55, "audioInsects": 0.5}
    for key, clip in loops.items():
        w = np.zeros(len(out))
        for j, h in enumerate(hours):
            w[j * seg:(j + 1) * seg] = living.evaluate(h)[key] * focus.get(key, 1.0)
        k = int(1.5 * SR)  # smooth the phase changes (moving average via cumulative sum)
        c = np.cumsum(np.pad(w, (k, k), mode="edge"))
        w = (c[2 * k:] - c[:-2 * k])[:len(out)] / (2 * k)
        out += np.tile(clip, len(out) // len(clip) + 1)[:len(out)] * w * 0.6
    for name, at in (("SFX_Rune_Chime", 3.0), ("SFX_Splash_Small", 8.0), ("SFX_Rock_Warm_Settle", 15.0),
                     ("SFX_Drink_Lap", 40.0), ("SFX_Flower_Rustle", 44.0)):
        c = clips[name] * 0.5
        s = int(at * SR)
        out[s:s + len(c)] += c[:len(out) - s]
    write_wav(path, limit(out, -1.0))
    return path


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "Audio"
    c = generate_all(out)
    if "--demo" in sys.argv:
        demo_mix(c, sys.argv[sys.argv.index("--demo") + 1])
    print("wrote", len(c), "clips ->", out)
