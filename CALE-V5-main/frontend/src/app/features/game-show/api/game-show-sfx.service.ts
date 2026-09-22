import { Injectable } from '@angular/core';

export type GameShowSfx =
  | 'buzz'
  | 'yourTurn'
  | 'correct'
  | 'strike'
  | 'strike2'
  | 'strike3'
  | 'steal'
  | 'stealFail'
  | 'round'
  | 'end'
  | 'tick'
  | 'drumroll'
  | 'confetti'
  | 'lightning';

/**
 * Lightweight Web Audio cues for projector/host — no asset files required.
 */
@Injectable({ providedIn: 'root' })
export class GameShowSfxService {
  private ctx: AudioContext | null = null;
  muted = false;

  unlock(): void {
    const ctx = this.ensureCtx();
    if (ctx.state === 'suspended') {
      void ctx.resume();
    }
  }

  /** Escalating strike cue: 1 mild, 2 deeper, 3 loud stinger. */
  playStrike(level: number): void {
    if (level >= 3) this.play('strike3');
    else if (level === 2) this.play('strike2');
    else this.play('strike');
  }

  play(kind: GameShowSfx): void {
    if (this.muted || typeof window === 'undefined') return;
    try {
      const ctx = this.ensureCtx();
      if (ctx.state === 'suspended') void ctx.resume();
      const now = ctx.currentTime;
      switch (kind) {
        case 'buzz':
          this.beep(ctx, now, 180, 0.22, 'sawtooth', 0.35);
          this.beep(ctx, now + 0.08, 140, 0.18, 'sawtooth', 0.28);
          break;
        case 'yourTurn':
          this.beep(ctx, now, 660, 0.1, 'square', 0.28);
          this.beep(ctx, now + 0.12, 880, 0.16, 'square', 0.3);
          break;
        case 'correct':
          this.beep(ctx, now, 523, 0.12, 'triangle', 0.22);
          this.beep(ctx, now + 0.1, 659, 0.14, 'triangle', 0.22);
          this.beep(ctx, now + 0.22, 784, 0.18, 'triangle', 0.2);
          break;
        case 'strike':
          this.beep(ctx, now, 140, 0.22, 'square', 0.28);
          break;
        case 'strike2':
          this.beep(ctx, now, 110, 0.26, 'square', 0.34);
          this.beep(ctx, now + 0.14, 90, 0.22, 'sawtooth', 0.3);
          break;
        case 'strike3':
          this.beep(ctx, now, 80, 0.18, 'sawtooth', 0.4);
          this.beep(ctx, now + 0.12, 55, 0.35, 'square', 0.45);
          this.beep(ctx, now + 0.32, 40, 0.4, 'sawtooth', 0.5);
          break;
        case 'steal':
          this.beep(ctx, now, 440, 0.1, 'sawtooth', 0.2);
          this.beep(ctx, now + 0.12, 554, 0.12, 'sawtooth', 0.22);
          this.beep(ctx, now + 0.26, 659, 0.16, 'sawtooth', 0.24);
          break;
        case 'stealFail':
          this.beep(ctx, now, 220, 0.15, 'square', 0.25);
          this.beep(ctx, now + 0.16, 160, 0.22, 'square', 0.28);
          break;
        case 'round':
          this.beep(ctx, now, 392, 0.1, 'sine', 0.18);
          this.beep(ctx, now + 0.12, 523, 0.14, 'sine', 0.18);
          break;
        case 'end':
          this.beep(ctx, now, 523, 0.12, 'triangle', 0.2);
          this.beep(ctx, now + 0.14, 659, 0.12, 'triangle', 0.2);
          this.beep(ctx, now + 0.28, 784, 0.12, 'triangle', 0.2);
          this.beep(ctx, now + 0.44, 1046, 0.28, 'triangle', 0.25);
          break;
        case 'tick':
          this.beep(ctx, now, 880, 0.04, 'sine', 0.08);
          break;
        case 'drumroll':
          for (let i = 0; i < 10; i++) {
            this.beep(ctx, now + i * 0.09, 90 + (i % 2) * 20, 0.07, 'square', 0.12 + i * 0.02);
          }
          this.beep(ctx, now + 0.95, 200, 0.2, 'triangle', 0.28);
          break;
        case 'confetti':
          this.beep(ctx, now, 784, 0.08, 'triangle', 0.2);
          this.beep(ctx, now + 0.08, 988, 0.1, 'triangle', 0.22);
          this.beep(ctx, now + 0.18, 1175, 0.16, 'sine', 0.24);
          break;
        case 'lightning':
          this.beep(ctx, now, 900, 0.06, 'sawtooth', 0.25);
          this.beep(ctx, now + 0.07, 600, 0.08, 'sawtooth', 0.28);
          this.beep(ctx, now + 0.16, 1200, 0.12, 'square', 0.3);
          break;
      }
    } catch {
      // Audio may be blocked; ignore.
    }
  }

  private ensureCtx(): AudioContext {
    if (!this.ctx) {
      const AC = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.ctx = new AC();
    }
    return this.ctx;
  }

  private beep(
    ctx: AudioContext,
    at: number,
    freq: number,
    duration: number,
    type: OscillatorType,
    gainValue: number
  ): void {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, at);
    gain.gain.setValueAtTime(0.0001, at);
    gain.gain.exponentialRampToValueAtTime(gainValue, at + 0.01);
    gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start(at);
    osc.stop(at + duration + 0.02);
  }
}
