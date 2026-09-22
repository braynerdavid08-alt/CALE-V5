import { Injectable } from '@angular/core';

export type GameShowSfx =
  | 'buzz'
  | 'correct'
  | 'strike'
  | 'steal'
  | 'stealFail'
  | 'round'
  | 'end'
  | 'tick';

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
        case 'correct':
          this.beep(ctx, now, 523, 0.12, 'triangle', 0.22);
          this.beep(ctx, now + 0.1, 659, 0.14, 'triangle', 0.22);
          this.beep(ctx, now + 0.22, 784, 0.18, 'triangle', 0.2);
          break;
        case 'strike':
          this.beep(ctx, now, 120, 0.28, 'square', 0.3);
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
