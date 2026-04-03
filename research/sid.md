# MOS 6581/8580 SID (Sound Interface Device) Reference

## 1. Overview

The MOS Technology 6581 Sound Interface Device (SID) is the sound chip built into the
Commodore 64. Designed by Bob Yannes in late 1981, the SID was conceived not as a simple
beep-generator but as a genuine single-chip music synthesizer. Yannes, who later founded
Ensoniq, incorporated features from professional analog synthesizers -- ADSR envelope
generators, ring modulation, oscillator sync, and a multimode analog filter -- into a
single 28-pin DIP IC. No competing home computer sound chip of the era offered anything
comparable.

### Key Specifications

| Feature               | Detail                                         |
|-----------------------|------------------------------------------------|
| Voices                | 3 independent oscillators                      |
| Waveforms per voice   | Triangle, sawtooth, pulse (variable width), noise |
| Phase accumulator     | 24-bit per oscillator                          |
| Frequency range       | ~0.06 Hz to ~3995 Hz (NTSC) / ~3848 Hz (PAL)  |
| Frequency resolution  | 16-bit register (65536 steps, linear)          |
| Envelope              | Independent ADSR per voice, 8-bit resolution   |
| Filter                | 12 dB/octave state-variable (LP/BP/HP/notch)   |
| Filter cutoff         | 11-bit register                                |
| Resonance             | 4-bit control                                  |
| Volume                | 4-bit master volume                            |
| Additional I/O        | 2x 8-bit paddle ADC, external audio input      |
| Register space        | 29 of 32 registers used ($D400-$D41C)          |

### 6581 vs 8580 at a Glance

| Aspect              | 6581 (original)                | 8580 (revised, ~1986)            |
|---------------------|--------------------------------|----------------------------------|
| Process             | NMOS, 12V DC supply            | HMOS-II, 9V DC supply            |
| Heat / durability   | Runs hot, ESD-sensitive        | Cooler, more durable             |
| Combined waveforms  | Highly irregular, bit-mixing   | Closer to AND spec, cleaner      |
| Filter curve        | Sigmoid on log scale, varies   | Linear, consistent between chips |
| Filter distortion   | Strong, "dirty" character      | Cleaner, less distortion         |
| Resonance behavior  | Lower max, threshold behavior  | Higher max, continuous response  |
| Background noise    | Higher (audio-in pin coupling) | Significantly lower              |
| D418 digi playback  | Works due to output DC bias    | Nearly inaudible (bias removed)  |

Despite its documented flaws, many SID musicians prefer the 6581 for its characteristic
filter distortion and gritty sonic character. The 6581's imperfections are often considered
features rather than defects.

### Why the SID Is Legendary

- It was the first home computer chip designed as a real synthesizer, not a tone generator.
- Its analog filter gives it an organic warmth no other 8-bit chip matched.
- Its bugs and quirks became creative tools -- the ADSR bug, filter distortion, combined
  waveform artifacts, and the D418 digi trick all became staples of C64 music.
- A thriving demoscene community has continuously pushed the chip's boundaries for over
  40 years, producing a library of over 50,000 SID tunes archived by the High Voltage SID
  Collection (HVSC).

---

## 2. Register Map

The SID occupies addresses `$D400`-`$D41C` in the C64 memory map (base address 54272 in
decimal). The full range `$D400`-`$D7FF` is allocated, with registers mirrored/repeated
every 32 bytes ($20). The chip has a 5-bit address bus (A0-A4), giving 32 possible
register slots, of which 29 are used.

### Voice 1: $D400-$D406

| Address | Name      | Bits  | R/W | Description                        |
|---------|-----------|-------|-----|------------------------------------|
| $D400   | FREQLO1   | 7-0   | W   | Voice 1 frequency, low byte       |
| $D401   | FREQHI1   | 7-0   | W   | Voice 1 frequency, high byte      |
| $D402   | PWLO1     | 7-0   | W   | Voice 1 pulse width, low byte     |
| $D403   | PWHI1     | 3-0   | W   | Voice 1 pulse width, high nybble (bits 7-4 unused) |
| $D404   | CR1       | 7-0   | W   | Voice 1 control register           |
| $D405   | AD1       | 7-0   | W   | Voice 1 attack / decay             |
| $D406   | SR1       | 7-0   | W   | Voice 1 sustain / release          |

### Voice 2: $D407-$D40D

| Address | Name      | Bits  | R/W | Description                        |
|---------|-----------|-------|-----|------------------------------------|
| $D407   | FREQLO2   | 7-0   | W   | Voice 2 frequency, low byte       |
| $D408   | FREQHI2   | 7-0   | W   | Voice 2 frequency, high byte      |
| $D409   | PWLO2     | 7-0   | W   | Voice 2 pulse width, low byte     |
| $D40A   | PWHI2     | 3-0   | W   | Voice 2 pulse width, high nybble  |
| $D40B   | CR2       | 7-0   | W   | Voice 2 control register           |
| $D40C   | AD2       | 7-0   | W   | Voice 2 attack / decay             |
| $D40D   | SR2       | 7-0   | W   | Voice 2 sustain / release          |

### Voice 3: $D40E-$D414

| Address | Name      | Bits  | R/W | Description                        |
|---------|-----------|-------|-----|------------------------------------|
| $D40E   | FREQLO3   | 7-0   | W   | Voice 3 frequency, low byte       |
| $D40F   | FREQHI3   | 7-0   | W   | Voice 3 frequency, high byte      |
| $D410   | PWLO3     | 7-0   | W   | Voice 3 pulse width, low byte     |
| $D411   | PWHI3     | 3-0   | W   | Voice 3 pulse width, high nybble  |
| $D412   | CR3       | 7-0   | W   | Voice 3 control register           |
| $D413   | AD3       | 7-0   | W   | Voice 3 attack / decay             |
| $D414   | SR3       | 7-0   | W   | Voice 3 sustain / release          |

### Filter and Volume: $D415-$D418

| Address | Name      | Bits  | R/W | Description                        |
|---------|-----------|-------|-----|------------------------------------|
| $D415   | FCLO      | 2-0   | W   | Filter cutoff frequency, low 3 bits (bits 7-3 unused) |
| $D416   | FCHI      | 7-0   | W   | Filter cutoff frequency, high 8 bits |
| $D417   | RES/FILT  | 7-0   | W   | Resonance and filter routing        |
| $D418   | MODE/VOL  | 7-0   | W   | Filter mode and master volume       |

### Miscellaneous / Read-Only: $D419-$D41C

| Address | Name      | Bits  | R/W | Description                        |
|---------|-----------|-------|-----|------------------------------------|
| $D419   | POTX      | 7-0   | R   | Paddle X position (8-bit ADC)      |
| $D41A   | POTY      | 7-0   | R   | Paddle Y position (8-bit ADC)      |
| $D41B   | OSC3      | 7-0   | R   | Voice 3 oscillator output readback |
| $D41C   | ENV3      | 7-0   | R   | Voice 3 envelope output readback   |

### Register Bit Details

#### Frequency Registers (FREQLO/FREQHI)

The 16-bit frequency value controls the oscillator pitch. The formula:

    Fout = (Fn * Fclk) / 16777216 Hz

Where:
- `Fn` = 16-bit frequency register value (0-65535)
- `Fclk` = system clock frequency

Practical formulas for computing register values from a desired frequency:

    PAL:   Fn = Fdesired * (18 * 2^24) / 17734475
    NTSC:  Fn = Fdesired * (14 * 2^24) / 14318182

Approximate shortcut (1 MHz clock): `Fout = Fn * 0.0596 Hz`

#### Pulse Width Registers (PWLO/PWHI)

12-bit value (0-4095) controlling the duty cycle of the pulse waveform:

    Duty Cycle = PW / 40.95 %

- PW = 0: always low (silent)
- PW = 2048 ($800): 50% square wave
- PW = 4095 ($FFF): always high (silent)

Only bits 0-3 of the high byte are used; bits 4-7 are ignored.

#### Control Register (CR) -- $D404 / $D40B / $D412

| Bit | Name  | Value | Description                                   |
|-----|-------|-------|-----------------------------------------------|
| 0   | GATE  | $01   | 1 = start attack/decay/sustain; 0 = start release |
| 1   | SYNC  | $02   | 1 = synchronize this oscillator with the preceding voice |
| 2   | RING  | $04   | 1 = ring-modulate this voice's triangle with preceding voice |
| 3   | TEST  | $08   | 1 = reset oscillator, halt waveform generation |
| 4   | TRI   | $10   | 1 = select triangle waveform                  |
| 5   | SAW   | $20   | 1 = select sawtooth waveform                  |
| 6   | PULSE | $40   | 1 = select pulse waveform                     |
| 7   | NOISE | $80   | 1 = select noise waveform                     |

Voice pairing for SYNC and RING:
- Voice 1 is synced/ring-modulated by **Voice 3**
- Voice 2 is synced/ring-modulated by **Voice 1**
- Voice 3 is synced/ring-modulated by **Voice 2**

The pairing is circular: 3->1, 1->2, 2->3.

#### Attack/Decay Register (AD) -- $D405 / $D40C / $D413

| Bits | Name   | Description                |
|------|--------|----------------------------|
| 7-4  | ATK    | Attack rate (0-15)         |
| 3-0  | DCY    | Decay rate (0-15)          |

#### Sustain/Release Register (SR) -- $D406 / $D40D / $D414

| Bits | Name   | Description                |
|------|--------|----------------------------|
| 7-4  | STN    | Sustain level (0-15)       |
| 3-0  | RLS    | Release rate (0-15)        |

#### Filter Cutoff ($D415-$D416)

11-bit value controlling the filter cutoff frequency. The low register ($D415) provides
only the bottom 3 bits (bits 0-2); the high register ($D416) provides the upper 8 bits.

    Effective cutoff = (FCHI << 3) | (FCLO & $07)

The 11-bit range maps differently on the two chip versions:
- **6581**: ~30 Hz to ~12 kHz, sigmoid response curve (highly variable between chips)
- **8580**: ~0 Hz to ~13 kHz, approximately linear response curve

#### Resonance / Filter Routing ($D417)

| Bits | Name   | Description                                    |
|------|--------|------------------------------------------------|
| 7-4  | RES    | Filter resonance (0-15). Higher values create a peak at the cutoff frequency. |
| 3    | FILTEX | 1 = route external audio input through filter  |
| 2    | FILT3  | 1 = route voice 3 through filter               |
| 1    | FILT2  | 1 = route voice 2 through filter               |
| 0    | FILT1  | 1 = route voice 1 through filter               |

Voices not routed through the filter are mixed directly to the output.

#### Mode / Volume ($D418)

| Bits | Name   | Description                                    |
|------|--------|------------------------------------------------|
| 7    | 3OFF   | 1 = disconnect voice 3 output (voice 3 still available as modulation source and via $D41B/$D41C readback) |
| 6    | HP     | 1 = enable high-pass filter                    |
| 5    | BP     | 1 = enable band-pass filter                    |
| 4    | LP     | 1 = enable low-pass filter                     |
| 3-0  | VOL    | Master volume (0-15)                           |

Multiple filter modes can be enabled simultaneously:
- LP + HP = notch (band-reject) filter
- LP + BP, HP + BP, or all three together are also valid combinations

#### Read-Only Registers

**POTX / POTY ($D419-$D41A):** 8-bit ADC values from the paddle/mouse inputs. The
measurement uses capacitor charge-time detection across a ~500k-ohm range. Values exhibit
jitter and typically require software filtering for smooth readings.

**OSC3 ($D41B):** Returns the upper 8 bits of voice 3's current oscillator output. This
provides a real-time snapshot of the waveform being generated. Commonly used as a
pseudo-random number source (when voice 3 is set to noise waveform) or as a modulation
source for software-driven effects.

**ENV3 ($D41C):** Returns the current 8-bit value of voice 3's envelope generator. Can be
used to read envelope state for software purposes, or as a slow modulation source.

---

## 3. Waveforms

Each SID voice generates one of four base waveforms, selected via bits 4-7 of the control
register. Multiple waveform bits can be set simultaneously to produce combined waveforms.

### Triangle (bit 4, $10)

Generated by XORing the upper bits of the 24-bit phase accumulator with its MSB (bit 23).
During the first half of the cycle the output ramps up; during the second half (when the
MSB is 1) the bits are inverted, creating the descending ramp. The result is a smooth
triangle wave.

- Harmonics: odd harmonics only (1st, 3rd, 5th...) falling off as 1/n^2
- Sound character: mellow, flute-like
- The triangle rises twice as fast as the sawtooth (completes a full up/down cycle in the
  same period the sawtooth completes one ramp)
- Only waveform affected by ring modulation

### Sawtooth (bit 5, $20)

The upper 12 bits of the 24-bit phase accumulator are output directly. The waveform ramps
linearly from minimum to maximum and then resets sharply.

- Harmonics: all harmonics (1st, 2nd, 3rd...) falling off as 1/n
- Sound character: bright, buzzy, rich
- The most harmonically rich of the periodic waveforms

### Pulse (bit 6, $40)

A comparator tests whether the upper 12 bits of the phase accumulator exceed the 12-bit
pulse width register. Output is all-high when the accumulator is above the threshold and
all-low when below.

- Harmonics: depend on duty cycle. At 50% (square wave), only odd harmonics. As the duty
  cycle narrows, even harmonics appear and the spectrum broadens.
- Sound character: hollow at 50%, increasingly nasal/reedy at narrower widths
- The only waveform with a continuously adjustable timbre parameter (pulse width)
- Pulse width modulation (PWM) is one of the most characteristic SID effects

### Noise (bit 7, $80)

Generated by a 23-bit Linear Feedback Shift Register (LFSR) using a Fibonacci
configuration. The LFSR taps bits 22 and 17, XORs them, and feeds the result back to
bit 0. The shift register is clocked by bit 19 of the phase accumulator (not the system
clock directly), so the noise "pitch" is controlled by the frequency register.

The 8-bit output is derived from bits 20, 18, 14, 11, 9, 5, 2, and 0 of the LFSR,
scrambled to provide good statistical distribution.

- Sound character: white noise (spectrally flat), with pitch control varying the "color"
- Higher frequency register values produce faster, brighter noise
- Used extensively for percussion, explosions, wind, and other non-pitched sounds

### Combined Waveforms

Setting multiple waveform bits simultaneously activates multiple waveform generators
feeding the DAC input through pass-transistor logic. The interaction depends on the
chip revision:

**On the 6581 (NMOS):** The driving strengths of outputs are asymmetric -- pulling low
is stronger than pulling high. When multiple waveforms are active, this creates complex
bit interactions where zeros in one waveform tend to dominate. The result is NOT a simple
AND or OR operation but a recursive mixing influenced by on-chip resistance and amplifier
thresholds. The output often has a fractal-like quality.

Notable 6581 combined waveform behaviors:
- **SAW + TRI**: Very quiet, almost unusable
- **PULSE + SAW**: Usable, never exceeds corresponding sawtooth value
- **PULSE + TRI**: Somewhat usable
- **PULSE + SAW + TRI**: Very quiet, almost unusable
- **NOISE + any other**: Dangerous -- see "noise zeroing" below

**On the 8580 (HMOS-II):** More stable waveform generators produce cleaner combined
waveforms that come closer to matching the documented AND behavior. All combinations are
more usable, and SAW+TRI produces audible output.

**Noise waveform zeroing:** Combining the noise waveform with any other waveform can
cause the pull-down transistors of the other waveform to zero out bits in the LFSR. Once
enough bits are cleared, the LFSR can become stuck at all-zeros and produce silence. This
is an irreversible state on the 6581 -- the only recovery is to set the TEST bit, which
injects a 1 and allows the LFSR to refill.

---

## 4. ADSR Envelope

Each of the three voices has an independent ADSR (Attack-Decay-Sustain-Release) envelope
generator that controls the voice amplitude over time.

### Envelope Phases

1. **Attack**: When the GATE bit is set to 1, the envelope counter ramps from its current
   value up to $FF (255) at a rate determined by the ATK setting.
2. **Decay**: Once the counter reaches $FF, it transitions to decay, ramping down toward
   the sustain level at a rate determined by the DCY setting.
3. **Sustain**: The counter holds at the sustain level (STN * 17, giving values $00, $11,
   $22 ... $FF) for as long as the GATE bit remains 1.
4. **Release**: When the GATE bit is cleared to 0, the counter ramps from its current
   value down to $00 at a rate determined by the RLS setting.

### Timing Table

Attack, decay, and release each offer 16 rate settings (0-15). Decay and release share
the same timing curve. These values assume approximately 1 MHz clock:

| Value | Attack Time | Decay/Release Time |
|-------|-------------|-------------------|
| 0     | 2 ms        | 6 ms              |
| 1     | 8 ms        | 24 ms             |
| 2     | 16 ms       | 48 ms             |
| 3     | 24 ms       | 72 ms             |
| 4     | 38 ms       | 114 ms            |
| 5     | 56 ms       | 168 ms            |
| 6     | 68 ms       | 204 ms            |
| 7     | 80 ms       | 240 ms            |
| 8     | 100 ms      | 300 ms            |
| 9     | 250 ms      | 750 ms            |
| 10    | 500 ms      | 1.5 s             |
| 11    | 800 ms      | 2.4 s             |
| 12    | 1 s         | 3 s               |
| 13    | 3 s         | 9 s               |
| 14    | 5 s         | 15 s              |
| 15    | 8 s         | 24 s              |

Note: The actual C64 clock is 985,248 Hz (PAL) or 1,022,727 Hz (NTSC), not exactly 1 MHz.

### Sustain Level Mapping

The 4-bit sustain value (0-15) maps to an 8-bit envelope level by replicating the nybble:

| STN | Envelope level |
|-----|----------------|
| 0   | $00            |
| 1   | $11            |
| 2   | $22            |
| 3   | $33            |
| 4   | $44            |
| 5   | $55            |
| 6   | $66            |
| 7   | $77            |
| 8   | $88            |
| 9   | $99            |
| A   | $AA            |
| B   | $BB            |
| C   | $CC            |
| D   | $DD            |
| E   | $EE            |
| F   | $FF            |

### Internal Mechanism

The envelope generator uses two counters:

**Rate counter (15-bit):** A prescaler that determines the speed of envelope changes. It
counts up on every phi2 clock cycle. When it reaches its period value, it wraps (the
counter is compared against the period with a 15-bit mask of $7FFF and an overflow mask
of $8000). The rate counter period values for each ADSR setting are:

| Setting | Rate Counter Period |
|---------|-------------------|
| 0       | 9                 |
| 1       | 32                |
| 2       | 63                |
| 3       | 95                |
| 4       | 149               |
| 5       | 220               |
| 6       | 267               |
| 7       | 313               |
| 8       | 392               |
| 9       | 977               |
| 10      | 1954              |
| 11      | 3126              |
| 12      | 3907              |
| 13      | 11720             |
| 14      | 19532             |
| 15      | 31251             |

**Exponential counter:** Modifies the effective rate during decay and release to produce
a non-linear (approximately exponential) curve. The exponential counter period changes at
specific envelope counter thresholds:

| Envelope counter value | Exponential period |
|-----------------------|-------------------|
| $FF                   | 1 (linear)        |
| $5D                   | 2                 |
| $36                   | 4                 |
| $1A                   | 8                 |
| $0E                   | 16                |
| $06                   | 30                |

During attack, the exponential counter period is always 1 (linear ramp). During decay and
release, the exponential counter introduces progressively slower changes as the envelope
value decreases, mimicking the exponential decay of natural sounds.

### The ADSR Bug

The most notorious SID quirk. The core problem: **ADSR rate counters are never reset.**

When a new note is triggered (GATE set to 1), it would be logical to reset the rate
counter to zero so that the attack phase begins with consistent timing. But the SID does
not do this. The rate counter simply continues from wherever it was.

**Consequences:**
- If the rate counter happens to be near its comparison point, the first envelope step
  occurs almost immediately. If it just passed the comparison point, there can be a
  delay of up to one full rate-counter period before the first step.
- With fast attack settings (low period values), this variation is imperceptible.
- With medium-to-slow attack settings, it can cause notes to sound weak, delayed, or
  entirely missed if the envelope does not reach audible levels before the note ends.

**Additional quirk:** In the first cycle of the attack and decay phases, the wrong rate
is used because the R0 direction line reacts with a one-cycle delay.

**The envelope counter can also wrap around:** If the counter is at $FF and an attack is
triggered, it can overflow past $FF to $00 and get stuck.

### Hard Restart

The standard workaround for the ADSR bug. The technique forces the envelope into a known
state before triggering a new note:

1. Two frames before the note: clear the GATE bit (start release)
2. Set ADSR to a fast release value (e.g., $F0 for sustain=F, release=0)
3. On the note frame: set the desired ADSR values and set GATE=1

This ensures the rate counter and envelope counter are in a predictable state. The
2-frame minimum ensures the envelope has time to reach $00 before the new attack begins.

Different music editors and players implement variations of this technique (some use
1-frame, 2-frame, or 3-frame hard restart windows), trading off reliability against the
audibility of the reset gap.

---

## 5. Filters

The SID includes an analog state-variable filter shared by all three voices (plus an
external audio input). It is a 12 dB/octave (2-pole) design providing simultaneous access
to low-pass, band-pass, and high-pass outputs.

### Filter Modes

| Mode      | Bit | $D418 value | Description                               |
|-----------|-----|-------------|-------------------------------------------|
| Low-pass  | 4   | $10         | Passes frequencies below the cutoff       |
| Band-pass | 5   | $20         | Passes frequencies near the cutoff        |
| High-pass | 6   | $40         | Passes frequencies above the cutoff       |
| Notch     | 4+6 | $50         | LP + HP combined: rejects frequencies near cutoff |

Additional combinations (LP+BP, HP+BP, LP+BP+HP) are also valid and produce various
blended filter responses.

### Filter Routing

Each voice can be independently routed through the filter or mixed directly to the
output using bits 0-2 of $D417. Voices not routed through the filter bypass it entirely
and are summed at the output.

The external audio input (pin 26) can also be routed through the filter (bit 3 of $D417).
On the 6581, this pin contributes noise even when no external signal is connected, which
is one source of the 6581's higher background noise. Disconnecting the pin reduces noise.

### Cutoff Frequency

The 11-bit cutoff value provides 2048 steps, but the actual response differs dramatically
between chip versions:

**6581:**
- The cutoff range follows a sigmoid curve on a logarithmic scale
- Approximately the bottom ~200 register values produce almost no change
- The useful range is roughly from register values ~200 to ~1800
- Response varies wildly between individual chips due to manufacturing tolerances
- Approximate usable range: ~200 Hz to ~8 kHz (chip-dependent)

**8580:**
- Nearly linear control curve
- Consistent response between chips
- Range extends closer to 0 Hz at the low end
- Upper limit reaches approximately 13 kHz
- Much closer to design specifications

### Resonance

The 4-bit resonance control (bits 4-7 of $D417) creates a peak at the cutoff frequency,
emphasizing frequencies near the cutoff point:

**6581:** Resonance exhibits threshold behavior -- low settings produce little effect,
then the resonance kicks in more abruptly at higher settings. Maximum self-oscillation
is harder to achieve.

**8580:** More continuous, predictable resonance response. Can achieve stronger resonance
peaks, including near-self-oscillation at maximum settings.

### The SID Filter Bug (6581)

The 6581 filter is widely considered "buggy" compared to the 8580, though many of its
characteristics are embraced as features:

- **Distortion:** At moderate to high resonance, the 6581 filter introduces significant
  non-linear distortion, particularly at low frequencies. This distortion gives the 6581
  its characteristic "dirty" filter sound and is exploited by musicians for effects
  resembling distorted electric guitar.
- **High-pass attenuation:** The high-pass output is approximately 3 dB lower than the
  other outputs, contributing to the 6581's bassier overall character.
- **Chip-to-chip variation:** Every 6581 has a slightly different filter curve, which
  means tunes optimized for one chip may sound different on another.
- **Output coupling:** The non-inverting amplifier stages introduce DC coupling artifacts
  that affect the mixer output.

### Voice 3 Off

Bit 7 of $D418 disconnects voice 3 from the audio output without affecting the
oscillator or envelope generator. This allows voice 3 to serve as a silent modulation
source: its oscillator output ($D41B) and envelope output ($D41C) can still be read
by software and used to modulate other parameters. However, if voice 3 is routed through
the filter, it still contributes to the filter's output even with 3OFF set.

---

## 6. Ring Modulation and Synchronization

### Ring Modulation

Ring modulation multiplies two signals, producing sum and difference frequencies. On the
SID, it works specifically with the triangle waveform:

When RING is enabled (bit 2 of the control register), the voice's triangle waveform
output is modified by XORing its phase-folding logic with the MSB (bit 23) of the
preceding voice's phase accumulator. This means:
- When the modulating voice's accumulator MSB = 0, the triangle behaves normally.
- When the modulating voice's accumulator MSB = 1, the triangle's ramp direction inverts,
  creating a discontinuity.

The result is a waveform with frequency content at the sum and difference of the two
voices' frequencies. If the two voices are detuned, this produces metallic, bell-like,
or dissonant tones depending on the frequency ratio.

**Voice pairing:**
- Voice 1's triangle is ring-modulated by **Voice 3**
- Voice 2's triangle is ring-modulated by **Voice 1**
- Voice 3's triangle is ring-modulated by **Voice 2**

**Usage notes:**
- Only the triangle waveform is affected. The RING bit has no effect on other waveforms.
- The modulating voice does not need to have its GATE set or produce audible output.
  Only its phase accumulator (oscillator) matters.
- Ring modulation can be combined with hard sync on the same voice for complex timbres.
- Setting the two voices to harmonically related frequencies (e.g., octaves, fifths)
  produces structured harmonic results; inharmonic ratios produce metallic or clangorous
  sounds useful for bells and effects.

### Hard Sync (Oscillator Synchronization)

When SYNC is enabled (bit 1 of the control register), this voice's phase accumulator is
forcibly reset to zero whenever the preceding voice's phase accumulator MSB (bit 23)
transitions from 0 to 1 (i.e., at the start of the second half of its cycle).

**Effect:** The synced voice's waveform is "restarted" at the syncing voice's rate. If
the synced voice has a higher frequency, it produces multiple partial cycles within each
sync period, creating a harmonically rich waveform whose harmonic content changes as the
synced voice's frequency is varied.

**Voice pairing (same as ring mod):**
- Voice 1 is synced by **Voice 3**
- Voice 2 is synced by **Voice 1**
- Voice 3 is synced by **Voice 2**

**Classic sync sweep:** Set one voice (the sync source) to a fixed low frequency and the
synced voice to a higher frequency. Sweeping the synced voice's frequency produces the
characteristic "sync sweep" -- a bright, vocal, resonant-sounding timbre change heard in
countless C64 tunes and analog synthesizers.

**Usage notes:**
- Hard sync works with all waveforms, not just triangle.
- The sync source voice can be silent (e.g., voice 3 with 3OFF set) while still providing
  the sync signal.
- Sync and ring mod can be active simultaneously on the same voice, using the same
  modulating voice, producing particularly complex timbres.

---

## 7. Music Techniques

### Arpeggios

With only three voices, polyphonic chords require creative workarounds. The arpeggio
technique rapidly cycles a single voice through two or three different pitch values at the
screen refresh rate (50 Hz PAL / 60 Hz NTSC) or faster. At sufficient speed, the human
ear blends the rapidly alternating notes into a perceived chord.

Common patterns:
- **Major chord:** root, +4 semitones, +7 semitones, repeat
- **Minor chord:** root, +3, +7
- **Power chord:** root, +7

The speed of cycling is critical. At 50 Hz (one note change per frame), three-note
arpeggios produce a warbling quality that is a defining element of the C64 sound. At 2x
speed (100 Hz), arpeggios sound smoother but consume more raster time.

### Pulse Width Modulation (PWM)

Continuously varying the pulse width register over time produces a phasing, chorus-like
animation of the timbre. This is one of the most common and characteristic SID effects.

Typical implementation: a software routine adds or subtracts a small value to/from the
pulse width register each frame, often ping-ponging between lower and upper bounds.
The rate and range of the sweep determine the character of the effect.

PWM is "the holy grail" of SID sound design -- cheap in CPU cycles, applicable to any
voice using the pulse waveform, and producing a lively, animated quality.

### Filter Sweeps

Gradually changing the filter cutoff frequency over time produces a "wah" or sweep effect.
Combined with resonance, this creates dramatic timbral changes.

- **Opening sweep:** Cutoff rises from low to high, progressively revealing harmonics
- **Closing sweep:** Cutoff falls from high to low, progressively muffling the sound
- **Resonant sweep:** High resonance with moving cutoff creates a sharp formant peak
  that sweeps through the spectrum

Filter sweeps are used for bass lines, lead transitions, build-ups, and atmospheric
effects. The 6581's filter distortion adds extra character to resonant sweeps.

### Multi-Speed Players

Standard C64 music players update the SID registers once per video frame (50 Hz PAL /
60 Hz NTSC). Multi-speed players call the update routine multiple times per frame using
raster interrupts:

- **1x:** Normal speed (50/60 Hz). Standard for most music.
- **2x:** 100/120 Hz. Smoother arpeggios and effects, costs more raster time.
- **4x:** 200/240 Hz. Very smooth effects, significant raster time cost.
- **8x and higher:** Used for extreme resolution, leaves very little time for other code.

The trade-off is raster time: each call to the music player consumes CPU cycles that
would otherwise be available for graphics, game logic, or other effects. A typical player
occupies 900-1000 bytes of code and consumes a few hundred to a thousand cycles per call.

Multi-speed tunes are flagged in the PSID/RSID file format header, allowing players to
call the play routine at the correct frequency.

### Digi/Sample Playback ($D418 Volume Trick)

The 6581's 4-bit volume register ($D418 bits 0-3) has a DC coupling artifact: changing
the volume directly modulates the audio output level. By writing sample values to the
volume register at a regular rate, the SID effectively becomes a 4-bit DAC, enabling
PCM sample playback.

**4-bit technique:**
- Write the upper nybble of each sample byte to $D418 (preserving filter mode bits)
- Update at 4-8 kHz for intelligible speech, 8-16 kHz for music-quality
- CPU cost: one write every ~60-120 cycles at 8 kHz
- Since each byte holds two 4-bit samples, memory usage is relatively efficient
- Electronic Speech Systems pioneered this in *Impossible Mission* (1984)
- Martin Galway famously used it for sampled drums in *Arkanoid* (1987)

**8580 compatibility:** The 8580 removed the DC bias that makes this trick work. To
restore the effect on 8580 chips:
- Hardware fix: solder a 470k-ohm resistor between the EXT IN pin and GND
- Software fix: configure all three voices to produce a DC offset by setting them to
  pulse waveform with the test bit enabled and maximum sustain ($49 control, $F0 sustain)

### 8-Bit Sample Playback (Mahoney/Soundemon Technique)

Discovered by the Finnish coder Soundemon in 2008 and later refined by Pex "Mahoney"
Tufvesson, this technique achieves true 8-bit sample resolution:

1. Set the TEST bit to reset the oscillator (phase accumulator goes to zero)
2. Briefly select the triangle waveform -- the accumulator ramps up linearly
3. After a precisely counted number of cycles, disable all waveforms
4. The DAC holds the last output value, which corresponds to the cycle count

By controlling exactly how many cycles the triangle waveform runs, you can set the
DAC output to any of 256 levels. This works because the triangle ramp is linear and
the DAC latches its last value when waveforms are disabled.

This technique can produce four software-mixed 8-bit channels with optional filtering,
plus two ordinary SID voices, though it is extremely CPU-intensive and requires precise
cycle counting.

### Combined Waveform Tricks

Setting multiple waveform bits produces unique timbres not achievable with single
waveforms. On the 6581, these are particularly unpredictable and sonically interesting:

- **PULSE + SAW ($60):** A bright, slightly hollowed sound. Popular for bass.
- **PULSE + TRI ($50):** A softer combined tone.
- **SAW + TRI ($30):** Very quiet on 6581, more usable on 8580.

Musicians exploit the 6581's irregular combined waveforms for specific timbres that
cannot be replicated on the 8580.

### Vibrato and Portamento

**Vibrato:** Rapid, small oscillation of the frequency register around the target note.
Implemented by adding/subtracting a small value to the frequency each frame, typically
using a sine or triangle LFO table. Depth (range of oscillation) and speed (rate of
oscillation) are controllable parameters.

**Portamento (slide/glide):** Smoothly transitioning from one frequency to another over
multiple frames. The frequency register is incremented or decremented each frame by a
calculated step size to reach the target pitch over the desired duration. Can be linear
(constant step size) or logarithmic (proportional step size for perceptually even slides).

---

## 8. Hardcore Details

### The ADSR Bug in Detail

The root cause is the 15-bit rate counter's non-resetting behavior. Here is the full
mechanism:

1. Each voice has a 15-bit rate counter that increments on every phi2 clock cycle
   (~1 MHz).
2. The rate counter is compared against a rate counter period value (see table in Section
   4) corresponding to the current ADSR phase's rate setting.
3. When the counter reaches the period value, the envelope counter increments (attack)
   or decrements (decay/release) by one step, and the rate counter wraps.
4. **The rate counter is never reset** -- not when the GATE bit changes, not when the
   ADSR settings change, not when a new note starts.

The consequence: when a new note begins (GATE 0->1), the rate counter could be anywhere
in its cycle. If the current rate counter value happens to be just past the period
comparison point, the first envelope step will not occur until the counter wraps all the
way around. For rate period 9 (attack=0), this is imperceptible. For rate period 31251
(attack=15), the worst-case delay is 31 milliseconds -- noticeable but not catastrophic.
The real problem emerges with medium-fast settings where the delay is long enough to
affect the perceived attack but short enough that musicians expect consistent timing.

Additionally, the ADSR phase transition (attack->decay, or gate-off->release) uses the
wrong rate for the first cycle because the internal R0 direction flag reacts with a
one-cycle delay.

The **envelope counter wrap-around** is a separate issue: if the counter is at $FF and
an attack is triggered, the counter can overflow past $FF. Since the attack phase
terminates when the counter equals $FF, the counter will ramp all the way through $00
and back up to $FF, producing a very long attack or an effectively silent note.

### 6581 vs 8580 Combined Waveform Differences

The difference stems from the manufacturing process:

**6581 (NMOS):** The pass-transistor waveform selectors have asymmetric drive strength.
Pull-downs (driving to 0) are strong, but pull-ups (driving to 1) are weak. When two
waveforms disagree on a bit value, the one driving 0 tends to win. Furthermore, adjacent
DAC bits influence each other through polysilicon resistance, creating recursive coupling
effects. The result:
- SAW+TRI and PULSE+SAW+TRI produce very low-amplitude, heavily distorted outputs
- Combined waveforms produce fractal-like patterns unique to each chip
- PULSE combined with other waveforms reduces overall loudness through bus resistance

**8580 (HMOS-II):** Improved manufacturing makes drive strengths more symmetric. Combined
waveforms approach the idealized AND behavior described in the original documentation.
All combinations are usable, and SAW+TRI produces meaningful output. The patterns are
cleaner but lack the "character" of 6581 combined waveforms.

### Filter Distortion on 6581

The 6581's filter produces distortion through several mechanisms:

- **Non-linear transconductance:** The OTA (Operational Transconductance Amplifier) stages
  in the filter are driven beyond their linear range at moderate signal levels, creating
  harmonic distortion.
- **Clipping:** At high resonance, the filter's internal signal levels can clip against
  the supply rails.
- **DC coupling:** The filter stages are DC-coupled, allowing offset voltages to
  accumulate and affect the operating point.
- **Capacitor-dependent response:** External capacitors (typically 470 pF on the C64
  board) interact with on-chip resistance variations to produce the characteristic
  filter curve.

Many musicians deliberately exploit this distortion. The 6581 filter distortion on bass
sounds, in particular, creates a gritty warmth that defines the classic C64 sound.

### Galway Noise / Noise Waveform Reset Trick

Named after legendary C64 composer Martin Galway, this technique exploits the behavior
of the noise LFSR in combination with waveform switching:

**The trick:** Set the voice to noise waveform for exactly one frame, then switch to a
pitched waveform (typically pulse or sawtooth). During the single frame of noise, the
percussive transient of the noise is heard. Immediately switching to a pitched waveform
with a rapid pitch drop simulates a drum strike:

- The noise provides the stick/attack transient
- The pitched waveform with fast pitch descent provides the body/resonance
- The ADSR envelope provides the decay

This creates convincing drum sounds from a single SID voice, leaving the other two voices
free for melodic content. Martin Galway and many other composers used variations of this
technique to create complete drum kits.

**Bass drum variant:** Use a square wave (pulse) with a very fast frequency sweep
downward, with only the first frame using noise for the transient attack.

**Snare variant:** Longer noise burst (2-3 frames) combined with a mid-frequency tone
sweep.

### Test Bit Tricks

The TEST bit (bit 3 of the control register) has several uses beyond its documented
purpose of halting the oscillator:

**Oscillator reset:** Setting TEST immediately zeroes the 24-bit phase accumulator. The
output level depends on the selected waveform:
- Triangle: output goes to 0
- Sawtooth: output goes to 0
- Pulse: output depends on pulse width setting (low if PW > 0)
- Noise: the LFSR is not clocked, but see below

**Noise LFSR recovery:** When noise is combined with other waveforms and the LFSR gets
zeroed, setting the TEST bit forces a 1 into the LFSR feedback path. Keeping TEST set
for approximately 8000 cycles fills the LFSR with 1s (reaching $7FFFFF), restoring it
to a functioning state.

**Sample playback:** The test bit is central to the Mahoney 8-bit playback technique
(see Section 7). It resets the oscillator so that a subsequent triangle waveform ramp
starts from a known zero point.

**Fast waveform zeroing:** Setting TEST and then immediately clearing it provides a way
to quickly silence an oscillator and restart it at phase 0, useful for tight
synchronization of musical events.

### Reading Voice 3 Output for Pseudo-Random Numbers

Register $D41B returns the upper 8 bits of voice 3's waveform output in real time. When
voice 3 is set to the noise waveform, $D41B provides a stream of pseudo-random bytes
from the LFSR.

This is the C64's most commonly used hardware random number generator:

```
    LDA #$FF       ; maximum frequency for fast noise
    STA $D40E
    STA $D40F
    LDA #$80       ; noise waveform, no gate
    STA $D412
    ; ...later...
    LDA $D41B      ; read random byte
```

The 3OFF bit ($D418 bit 7) can be used to silence voice 3's audio output while still
allowing $D41B reads, so the random number generator does not produce audible noise.

The quality of randomness depends on the frequency setting -- higher frequencies clock
the LFSR faster, producing more random-seeming values. At low frequencies, consecutive
reads may return the same value.

### Exact Timing of SID Updates

The SID reads its registers once per phi2 clock cycle (~1 MHz). Writes to SID registers
take effect on the next phi2 cycle after the CPU write cycle.

Important timing considerations:

- **Register writes are immediate:** Changes to frequency, pulse width, control, and
  ADSR registers take effect within one clock cycle.
- **VIC-II bus stealing:** The VIC-II graphics chip halts the CPU during DMA cycles
  (badlines, sprite fetch). During these periods, SID register writes are delayed until
  the CPU resumes. On text screen lines, the CPU loses ~40 cycles per badline. Each
  active sprite costs an additional 2-19 cycles.
- **Phase accumulator update:** The oscillator phase accumulator is updated every phi2
  cycle, giving sample-accurate frequency changes.
- **Filter settling:** The analog filter needs time to settle after cutoff or resonance
  changes. Rapid filter parameter changes can produce clicking or transient artifacts.
- **Envelope update:** The envelope generator operates on the phi2 clock, with effective
  rates determined by the rate counter (see Section 4).

For cycle-exact music players, the interaction between CPU timing, VIC-II DMA, and SID
updates must be carefully managed.

### 4-Bit Digi Playback via Volume Register

**Mechanism:** The volume register at $D418 (bits 0-3) controls a resistor ladder DAC
that sets the master output level. On the 6581, this DAC has a DC offset -- changing the
volume from one value to another produces a voltage step in the audio output proportional
to the difference. By rapidly writing successive sample values, arbitrary waveforms can
be reproduced.

**Sample rates and quality:**
- At 8 kHz: ~123 cycles per sample (PAL), adequate for speech and basic drums
- At 4 kHz: ~246 cycles per sample, intelligible speech
- At 16 kHz: ~62 cycles per sample, near-impossible without disabling display

**Data packing:** Two 4-bit samples per byte. The upper nybble is written first (shifted
right 4 or using a lookup table), then the lower nybble.

**Implementation approaches:**
1. **Cycle-counting:** Disable display ($D011 bit 4 = 0) and use exact cycle counts
   between writes. Most precise but sacrifices all graphics.
2. **Timer-driven NMI:** Use CIA#2 Timer A to fire NMI interrupts at the sample rate.
   Allows coexistence with raster interrupts and display. The NMI handler writes the
   next sample value and returns. Overhead: ~30-40 cycles per interrupt.

**Historical notes:**
- *Impossible Mission* (1984): first commercial use (Electronic Speech Systems)
- *Ghostbusters* (1984): sampled speech
- *Arkanoid* (1987, Martin Galway): sampled drum sounds integrated with SID music

### 8-Bit Digi Playback Techniques

Beyond the 4-bit volume register method, several techniques achieve higher resolution:

**Mahoney/Soundemon technique (2008):**
1. Set TEST bit to zero the phase accumulator
2. Select triangle waveform -- accumulator begins linear ramp
3. After N cycles (where N encodes the desired sample value), deselect all waveforms
4. The DAC holds the triangle's last value for ~2 scanlines
5. Repeat for each sample

The triangle waveform ramps through its full range in 256 cycles (it rises twice as fast
as sawtooth). By timing the waveform disable precisely, any of 256 levels can be latched.

Capabilities: four software-mixed 8-bit channels + two regular SID voices + optional
filtering. Cost: extremely CPU-intensive, requiring precise cycle counting.

**PWM-based technique:**
Uses rapid modulation of the pulse width register to simulate higher-resolution output.
The pulse waveform's amplitude depends on the pulse width setting, so sweeping pulse
width effectively provides amplitude control beyond 4 bits.

### 2SID and 3SID Configurations

The standard C64 has one SID at $D400. Additional SID chips can be added via hardware
modifications or cartridges:

**2SID (Dual SID):**
- Second SID typically mapped at $D420 (most common) or $DE00, $DF00
- Hardware solutions: SID2SID board, SIDFX, ARM2SID
- Provides 6 voices, 2 independent filters
- Used for stereo output (left/right panning) or simply more voices
- Most 2SID tunes in HVSC use $D420

**3SID (Triple SID):**
- Third SID at an additional address (varies by hardware)
- 9 voices, 3 independent filters
- ARM2SID and similar modern solutions support this configuration
- Allows truly rich arrangements with dedicated bass, lead, and chord voices

**Address detection:** The PSID/RSID file format header contains fields for the second
SID address (offset $7A) and third SID address (offset $7B), allowing emulators and
hardware players to configure correctly.

**Software considerations:** Music written for multi-SID requires hardware that supports
the specific address configuration. Players and trackers (e.g., SID Factory II) have
dedicated multi-SID support. Addresses in the $DE00-$DFFF I/O expansion space are the
most compatible across different hardware solutions.

---

## References

- [MOS Technology 6581 -- Wikipedia](https://en.wikipedia.org/wiki/MOS_Technology_6581)
- [SID -- C64-Wiki](https://www.c64-wiki.com/wiki/SID)
- [ADSR -- C64-Wiki](https://www.c64-wiki.com/wiki/ADSR)
- [SID Register Reference -- oxyron.de](https://www.oxyron.de/html/registers_sid.html)
- [Commodore SID 6581 Datasheet -- Waiting for Friday](https://www.waitingforfriday.com/?p=661)
- [MOS 6581 SID Datasheet (PDF) -- archive.6502.org](http://archive.6502.org/datasheets/mos_6581_sid.pdf)
- [SID Internals: ADSR Registers -- libsidplayfp Wiki](https://sourceforge.net/p/sidplay-residfp/wiki/SID%20internals%20-%20ADSR%20registers/)
- [reSID Envelope Implementation (rate counter tables) -- docs.rs](https://docs.rs/resid-rs/1.0.3/src/resid/envelope.rs.html)
- [SID Article by Imre Olajos -- GitHub](https://github.com/ImreOlajos/SID-Article/blob/main/SID-Article.md)
- [Classic Hard-Restart and ADSR -- Codebase64](http://www.codebase64.org/doku.php?id=base%3Aclassic_hard-restart_and_about_adsr_in_generally)
- [How to Calculate SID Frequency Tables -- Codebase64](https://codebase64.org/doku.php?id=base:how_to_calculate_your_own_sid_frequency_table)
- [C64 Sound Frequencies -- sta.c64.org](https://sta.c64.org/cbm64sndfreq.html)
- [Digital Sound Playback on the C64 -- Bumbershoot Software](https://bumbershootsoft.wordpress.com/2022/12/30/digital-sound-playback-on-the-c64/)
- [Driving the SID Chip -- G|A|M|E Journal](https://www.gamejournal.it/driving-the-sid-chip-assembly-language-composition-and-sound-design-for-the-c64/)
- [Sounds of the SID -- Dan Sanderson](https://dansanderson.com/mega65/sounds-of-the-sid/)
- [SID Schematics (Reverse-Engineered) -- GitHub](https://github.com/libsidplayfp/SID_schematics)
- [High Voltage SID Collection (HVSC)](https://www.hvsc.c64.org/)
- [SIDFX -- Dual SID Solution](https://www.sidfx.dk/)
