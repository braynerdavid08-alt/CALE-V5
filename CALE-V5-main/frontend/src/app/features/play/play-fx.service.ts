import { Injectable, signal } from '@angular/core';
import { playAnswerSound, preloadAnswerSounds } from '../../core/sound/answer-sounds';

export type PlaySound = 'correct' | 'wrong' | 'win' | 'badge' | 'tick' | 'start' | 'notify';

const MUTE_KEY = 'cale.play.muted';
const CONFETTI_COLORS = ['#1a6b8a', '#4eb6d4', '#f59e0b', '#22c55e', '#ef4444', '#8b5cf6', '#facc15'];

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  rotation: number;
  spin: number;
  color: string;
}

/** Confetti and sound cues for the practice games (answer sounds are audio files, the rest Web Audio). */
@Injectable({ providedIn: 'root' })
export class PlayFxService {
  readonly muted = signal(this.readMuted());
  private ctx: AudioContext | null = null;

  constructor() {
    preloadAnswerSounds();
  }

  toggleMute(): void {
    const next = !this.muted();
    this.muted.set(next);
    try {
      localStorage.setItem(MUTE_KEY, next ? '1' : '0');
    } catch {
      // Storage may be unavailable (private mode).
    }
  }

  play(kind: PlaySound): void {
    if (this.muted() || typeof window === 'undefined') return;
    if (kind === 'correct' || kind === 'wrong') {
      playAnswerSound(kind, 0.9, () => this.synth(kind));
      return;
    }
    this.synth(kind);
  }

  private synth(kind: PlaySound): void {
    try {
      const ctx = this.ensureCtx();
      if (ctx.state === 'suspended') void ctx.resume();
      const t = ctx.currentTime;
      switch (kind) {
        case 'correct':
          this.beep(ctx, t, 587, 0.1, 'triangle', 0.2);
          this.beep(ctx, t + 0.09, 880, 0.16, 'triangle', 0.2);
          break;
        case 'wrong':
          this.beep(ctx, t, 220, 0.14, 'square', 0.14);
          this.beep(ctx, t + 0.12, 165, 0.2, 'square', 0.14);
          break;
        case 'win':
          [523, 659, 784, 1046].forEach((f, i) => this.beep(ctx, t + i * 0.12, f, 0.2, 'triangle', 0.22));
          break;
        case 'badge':
          this.beep(ctx, t, 784, 0.08, 'sine', 0.2);
          this.beep(ctx, t + 0.08, 988, 0.1, 'sine', 0.22);
          this.beep(ctx, t + 0.18, 1319, 0.24, 'sine', 0.22);
          break;
        case 'tick':
          this.beep(ctx, t, 880, 0.04, 'sine', 0.07);
          break;
        case 'start':
          this.beep(ctx, t, 440, 0.08, 'square', 0.14);
          this.beep(ctx, t + 0.1, 660, 0.12, 'square', 0.16);
          break;
        case 'notify':
          this.beep(ctx, t, 880, 0.12, 'sine', 0.22);
          this.beep(ctx, t + 0.14, 1175, 0.22, 'sine', 0.2);
          break;
      }
    } catch {
      // Audio can be blocked by the browser.
    }
  }

  confetti(pieces = 140): void {
    if (typeof document === 'undefined') return;
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return;

    const canvas = document.createElement('canvas');
    canvas.setAttribute('aria-hidden', 'true');
    Object.assign(canvas.style, {
      position: 'fixed',
      inset: '0',
      width: '100vw',
      height: '100vh',
      pointerEvents: 'none',
      zIndex: '9999'
    });
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = window.innerWidth * dpr;
    canvas.height = window.innerHeight * dpr;
    document.body.appendChild(canvas);
    const g = canvas.getContext('2d');
    if (!g) {
      canvas.remove();
      return;
    }
    g.scale(dpr, dpr);

    const w = window.innerWidth;
    const h = window.innerHeight;
    const particles: Particle[] = Array.from({ length: pieces }, () => ({
      x: w / 2 + (Math.random() - 0.5) * w * 0.4,
      y: h * 0.35 + (Math.random() - 0.5) * 60,
      vx: (Math.random() - 0.5) * 14,
      vy: -Math.random() * 13 - 4,
      size: 6 + Math.random() * 7,
      rotation: Math.random() * Math.PI,
      spin: (Math.random() - 0.5) * 0.3,
      color: CONFETTI_COLORS[Math.floor(Math.random() * CONFETTI_COLORS.length)]
    }));

    const started = performance.now();
    const frame = (now: number) => {
      const elapsed = now - started;
      g.clearRect(0, 0, w, h);
      g.globalAlpha = elapsed > 2200 ? Math.max(0, 1 - (elapsed - 2200) / 800) : 1;
      for (const p of particles) {
        p.vy += 0.32;
        p.vx *= 0.99;
        p.x += p.vx;
        p.y += p.vy;
        p.rotation += p.spin;
        g.save();
        g.translate(p.x, p.y);
        g.rotate(p.rotation);
        g.fillStyle = p.color;
        g.fillRect(-p.size / 2, -p.size / 4, p.size, p.size / 2);
        g.restore();
      }
      if (elapsed < 3000) {
        requestAnimationFrame(frame);
      } else {
        canvas.remove();
      }
    };
    requestAnimationFrame(frame);
  }

  celebrate(): void {
    this.play('win');
    this.confetti();
  }

  private readMuted(): boolean {
    try {
      return localStorage.getItem(MUTE_KEY) === '1';
    } catch {
      return false;
    }
  }

  private ensureCtx(): AudioContext {
    if (!this.ctx) {
      const AC = window.AudioContext
        || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.ctx = new AC();
    }
    return this.ctx;
  }

  private beep(ctx: AudioContext, at: number, freq: number, duration: number, type: OscillatorType, volume: number): void {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, at);
    gain.gain.setValueAtTime(0.0001, at);
    gain.gain.exponentialRampToValueAtTime(volume, at + 0.01);
    gain.gain.exponentialRampToValueAtTime(0.0001, at + duration);
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start(at);
    osc.stop(at + duration + 0.02);
  }
}
