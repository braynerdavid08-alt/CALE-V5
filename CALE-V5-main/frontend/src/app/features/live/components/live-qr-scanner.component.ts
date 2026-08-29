import {
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  inject,
  output,
  signal
} from '@angular/core';
import { Html5Qrcode } from 'html5-qrcode';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { parseLiveJoinCode } from '../utils/parse-live-join-code';

@Component({
  selector: 'app-live-qr-scanner',
  standalone: true,
  imports: [UiButtonComponent],
  template: `
    <div class="scanner">
      <div #reader id="live-qr-reader" class="reader"></div>
      @if (error()) {
        <p class="error">{{ error() }}</p>
      }
      <ui-button type="button" variant="ghost" (click)="stop()">Detener cámara</ui-button>
    </div>
  `,
  styles: `
    .scanner {
      display: grid;
      gap: 0.75rem;
    }

    .reader {
      width: 100%;
      min-height: 240px;
      border-radius: 12px;
      overflow: hidden;
      border: 1px solid #2a3441;
      background: #0b1016;
    }

    .error {
      margin: 0;
      color: #f87171;
      font-size: 0.9rem;
    }
  `
})
export class LiveQrScannerComponent implements OnInit, OnDestroy {
  @ViewChild('reader', { static: true }) readerRef!: ElementRef<HTMLElement>;

  readonly codeScanned = output<string>();
  readonly error = signal<string | null>(null);

  private scanner: Html5Qrcode | null = null;
  private active = false;

  ngOnInit(): void {
    void this.start();
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  async start(): Promise<void> {
    if (this.active) {
      return;
    }
    this.error.set(null);
    const elementId = this.readerRef.nativeElement.id || 'live-qr-reader';
    this.scanner = new Html5Qrcode(elementId);
    try {
      await this.scanner.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 220, height: 220 } },
        (decoded) => this.onDecoded(decoded),
        () => { /* ignore scan misses */ }
      );
      this.active = true;
    } catch {
      this.error.set(
        'No se pudo abrir la cámara. Revisa los permisos o usa el código manual.'
      );
    }
  }

  async stop(): Promise<void> {
    if (!this.scanner || !this.active) {
      return;
    }
    try {
      await this.scanner.stop();
      await this.scanner.clear();
    } catch {
      /* ignore cleanup errors */
    }
    this.active = false;
    this.scanner = null;
  }

  private onDecoded(raw: string): void {
    const code = parseLiveJoinCode(raw);
    if (!code) {
      return;
    }
    this.codeScanned.emit(code);
    void this.stop();
  }
}
