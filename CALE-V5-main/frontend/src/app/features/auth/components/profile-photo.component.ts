import {
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  computed,
  inject,
  signal
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MeResponse } from '../../../core/auth/session.models';
import { SessionStore } from '../../../core/auth/session.store';
import { env } from '../../../core/config/env';
import { mapApiError } from '../../../core/http/map-api-error';
import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import { UiButtonComponent } from '../../../shared/ui/ui-button.component';
import { UiCardComponent } from '../../../shared/ui/ui-card.component';
import { UiIconComponent } from '../../../shared/ui/ui-icon.component';

/** Output size of the stored square photo (px). */
const PHOTO_SIZE = 512;

type Step = 'camera' | 'preview';

@Component({
  selector: 'app-profile-photo',
  standalone: true,
  imports: [UiButtonComponent, UiCardComponent, UiIconComponent],
  template: `
    <ui-card>
      <div class="photo-card">
        <div class="avatar-lg" aria-hidden="true">
          @if (photoSrc()) {
            <img [src]="photoSrc()" alt="" />
          } @else {
            <span>{{ initials() }}</span>
          }
        </div>
        <div class="body">
          <h2>Tu foto de perfil</h2>
          <p class="muted">Así te reconocen tu escuela y tus instructores.</p>
          @if (error()) {
            <p class="msg error" role="alert">{{ error() }}</p>
          }
          @if (notice()) {
            <p class="msg ok" role="status">{{ notice() }}</p>
          }
          <div class="actions">
            <ui-button type="button" [loading]="saving()" (click)="openCamera()">
              <ui-icon name="camera" /> Tomar foto
            </ui-button>
            <ui-button type="button" variant="secondary" [disabled]="saving()" (click)="galleryInput.click()">
              <ui-icon name="image" /> Elegir de mis fotos
            </ui-button>
            @if (photoSrc()) {
              <ui-button type="button" variant="ghost" [disabled]="saving()" (click)="remove()">
                Quitar foto
              </ui-button>
            }
          </div>
        </div>
      </div>
      <input #galleryInput type="file" accept="image/*" hidden (change)="onFile($event)" />
      <input #cameraInput type="file" accept="image/*" capture="user" hidden (change)="onFile($event)" />
    </ui-card>

    @if (step(); as s) {
      <div class="overlay" (click)="close()">
        <div
          class="sheet"
          role="dialog"
          aria-modal="true"
          aria-labelledby="photo-dialog-title"
          (click)="$event.stopPropagation()">
          <h2 id="photo-dialog-title">{{ s === 'camera' ? 'Mira a la cámara' : '¿Te gusta esta foto?' }}</h2>
          <div class="frame">
            @if (s === 'camera') {
              <video #video autoplay playsinline muted></video>
            } @else {
              <img [src]="previewUrl()" alt="Vista previa de tu foto" />
            }
          </div>
          @if (error()) {
            <p class="msg error" role="alert">{{ error() }}</p>
          }
          <div class="sheet-actions">
            @if (s === 'camera') {
              <ui-button type="button" (click)="capture()">
                <ui-icon name="camera" /> Tomar foto
              </ui-button>
              <ui-button type="button" variant="ghost" (click)="close()">Cancelar</ui-button>
            } @else {
              <ui-button type="button" [loading]="saving()" (click)="confirm()">Usar esta foto</ui-button>
              <ui-button type="button" variant="secondary" [disabled]="saving()" (click)="retake()">
                {{ fromCamera() ? 'Tomar otra' : 'Elegir otra' }}
              </ui-button>
              <ui-button type="button" variant="ghost" [disabled]="saving()" (click)="close()">Cancelar</ui-button>
            }
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .photo-card {
      display: flex;
      align-items: center;
      gap: 1.25rem;
      flex-wrap: wrap;
    }
    .avatar-lg {
      width: 7.5rem;
      height: 7.5rem;
      flex-shrink: 0;
      border-radius: 50%;
      overflow: hidden;
      display: grid;
      place-items: center;
      background: var(--color-primary-soft);
      color: var(--color-primary);
      font-size: 2.4rem;
      font-weight: 800;
      border: 3px solid color-mix(in srgb, var(--color-primary) 35%, transparent);
    }
    .avatar-lg img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .body { flex: 1; min-width: 14rem; display: grid; gap: 0.5rem; }
    h2 { margin: 0; font-size: var(--text-lg); }
    .muted { margin: 0; color: var(--color-text-secondary); }
    .msg { margin: 0; font-weight: 600; }
    .msg.error { color: var(--color-danger); }
    .msg.ok { color: var(--color-success, var(--color-primary)); }
    .actions { display: flex; flex-wrap: wrap; gap: 0.6rem; margin-top: 0.35rem; }
    .actions ui-icon, .sheet-actions ui-icon { margin-right: 0.35rem; }

    .overlay {
      position: fixed;
      inset: 0;
      z-index: var(--z-modal);
      background: var(--color-scrim);
      display: grid;
      place-items: center;
      padding: var(--spacing-md);
    }
    .sheet {
      width: min(440px, 100%);
      max-height: 100%;
      overflow: auto;
      background: var(--color-surface);
      color: var(--color-text);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      padding: var(--spacing-lg);
      box-shadow: var(--shadow-md);
      display: grid;
      gap: 1rem;
      text-align: center;
    }
    .sheet h2 { margin: 0; font-size: var(--text-lg); }
    .frame {
      width: min(100%, 20rem);
      aspect-ratio: 1;
      margin: 0 auto;
      border-radius: 50%;
      overflow: hidden;
      background: #000;
      border: 4px solid var(--color-primary);
    }
    .frame video, .frame img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .frame video { transform: scaleX(-1); }
    .sheet-actions { display: grid; gap: 0.6rem; }
    .sheet-actions ui-button { width: 100%; }

    @media (max-width: 480px) {
      .photo-card { flex-direction: column; text-align: center; }
      .actions { justify-content: center; }
      .actions ui-button { width: 100%; }
    }
  `]
})
export class ProfilePhotoComponent implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);

  @ViewChild('cameraInput') private cameraInput?: ElementRef<HTMLInputElement>;
  @ViewChild('galleryInput') private galleryInput?: ElementRef<HTMLInputElement>;
  @ViewChild('video') private set videoRef(ref: ElementRef<HTMLVideoElement> | undefined) {
    this.video = ref?.nativeElement;
    if (this.video && this.stream) {
      this.video.srcObject = this.stream;
      void this.video.play().catch(() => undefined);
    }
  }

  readonly step = signal<Step | null>(null);
  readonly previewUrl = signal<string | null>(null);
  readonly fromCamera = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly photoSrc = computed(() => resolveMediaUrl(this.session.user()?.photoUrl));
  readonly initials = computed(() => {
    const parts = (this.session.user()?.name ?? '').trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase() || '?';
  });

  private video?: HTMLVideoElement;
  private stream: MediaStream | null = null;
  private pending: Blob | null = null;

  ngOnDestroy(): void {
    this.stopCamera();
    this.revokePreview();
  }

  async openCamera(): Promise<void> {
    this.error.set(null);
    this.notice.set(null);
    if (!navigator.mediaDevices?.getUserMedia) {
      this.cameraInput?.nativeElement.click();
      return;
    }
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 1280 } },
        audio: false
      });
      this.fromCamera.set(true);
      this.step.set('camera');
    } catch {
      this.stopCamera();
      // Phones without camera permission in the browser can still open the camera app.
      if (matchMedia('(pointer: coarse)').matches) {
        this.cameraInput?.nativeElement.click();
        return;
      }
      this.error.set('No pudimos abrir la cámara. Revisa el permiso del navegador o elige una foto guardada.');
    }
  }

  async capture(): Promise<void> {
    const video = this.video;
    if (!video || video.videoWidth < 16) {
      this.error.set('La cámara todavía se está preparando. Espera un momento e inténtalo otra vez.');
      return;
    }
    this.error.set(null);
    const blob = await squareJpeg(video, video.videoWidth, video.videoHeight, true);
    this.stopCamera();
    this.showPreview(blob);
  }

  async onFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    this.error.set(null);
    this.notice.set(null);
    if (!file.type.startsWith('image/')) {
      this.error.set('Ese archivo no es una foto.');
      return;
    }
    try {
      const img = await loadImage(file);
      const blob = await squareJpeg(img, img.naturalWidth, img.naturalHeight, false);
      this.fromCamera.set(input === this.cameraInput?.nativeElement);
      this.showPreview(blob);
    } catch {
      this.error.set('No pudimos leer esa foto. Prueba con otra.');
    }
  }

  retake(): void {
    this.revokePreview();
    this.pending = null;
    this.step.set(null);
    if (this.fromCamera()) {
      void this.openCamera();
    } else {
      this.galleryInput?.nativeElement.click();
    }
  }

  confirm(): void {
    if (!this.pending) {
      return;
    }
    const form = new FormData();
    form.append('file', this.pending, 'perfil.jpg');
    this.saving.set(true);
    this.http.post<MeResponse>(`${env.apiUrl}/api/auth/me/photo`, form).subscribe({
      next: (me) => {
        this.saving.set(false);
        this.session.patchUser({ photoUrl: me.photoUrl ?? null });
        this.close();
        this.notice.set('¡Listo! Tu foto quedó guardada.');
      },
      error: (err) => {
        this.saving.set(false);
        this.close();
        this.error.set(mapApiError(err));
      }
    });
  }

  remove(): void {
    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.http.delete<MeResponse>(`${env.apiUrl}/api/auth/me/photo`).subscribe({
      next: () => {
        this.saving.set(false);
        this.session.patchUser({ photoUrl: null });
        this.notice.set('Quitamos tu foto.');
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(mapApiError(err));
      }
    });
  }

  close(): void {
    this.stopCamera();
    this.revokePreview();
    this.pending = null;
    this.step.set(null);
  }

  private showPreview(blob: Blob): void {
    this.revokePreview();
    this.pending = blob;
    this.previewUrl.set(URL.createObjectURL(blob));
    this.step.set('preview');
  }

  private revokePreview(): void {
    const url = this.previewUrl();
    if (url) {
      URL.revokeObjectURL(url);
    }
    this.previewUrl.set(null);
  }

  private stopCamera(): void {
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
    if (this.video) {
      this.video.srcObject = null;
    }
  }
}

function loadImage(file: Blob): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve(img);
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error('image_load_failed'));
    };
    img.src = url;
  });
}

/** Center-crops to a square, scales to {@link PHOTO_SIZE} and encodes as JPEG. */
function squareJpeg(
  source: CanvasImageSource,
  width: number,
  height: number,
  mirror: boolean
): Promise<Blob> {
  const side = Math.min(width, height);
  const canvas = document.createElement('canvas');
  canvas.width = PHOTO_SIZE;
  canvas.height = PHOTO_SIZE;
  const ctx = canvas.getContext('2d')!;
  if (mirror) {
    ctx.translate(PHOTO_SIZE, 0);
    ctx.scale(-1, 1);
  }
  ctx.drawImage(source, (width - side) / 2, (height - side) / 2, side, side, 0, 0, PHOTO_SIZE, PHOTO_SIZE);
  return new Promise((resolve, reject) =>
    canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('encode_failed'))), 'image/jpeg', 0.86)
  );
}
