import { Observable, defer, from } from 'rxjs';

/** Longest side after resizing; keeps sign details readable on large screens. */
const MAX_SIDE = 1600;
/** Files already this small and within MAX_SIDE are sent untouched. */
const SKIP_BELOW_BYTES = 250 * 1024;
const QUALITY = 0.82;

/**
 * Shrinks a picked image in the browser before upload: max 1600 px per side, re-encoded as WebP
 * (PNG/JPEG fallback when the browser can't encode WebP). GIFs keep their animation and are left as is.
 * Never returns a bigger file than the original, and falls back to the original on any failure.
 */
export function compressImage(file: File): Observable<File> {
  return defer(() => from(shrink(file).catch(() => file)));
}

async function shrink(file: File): Promise<File> {
  if (!file.type.startsWith('image/') || file.type === 'image/gif' || file.type === 'image/svg+xml') {
    return file;
  }

  const bitmap = await loadImage(file);
  const { width, height } = bitmap;
  const scale = Math.min(1, MAX_SIDE / Math.max(width, height));
  if (scale === 1 && file.size <= SKIP_BELOW_BYTES) {
    release(bitmap);
    return file;
  }

  const w = Math.max(1, Math.round(width * scale));
  const h = Math.max(1, Math.round(height * scale));
  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    release(bitmap);
    return file;
  }
  ctx.imageSmoothingQuality = 'high';
  ctx.drawImage(bitmap, 0, 0, w, h);
  release(bitmap);

  let blob = await toBlob(canvas, 'image/webp');
  if (!blob || blob.type !== 'image/webp') {
    // PNG keeps transparency (signs often have it); photos go to JPEG.
    blob = await toBlob(canvas, file.type === 'image/png' ? 'image/png' : 'image/jpeg');
  }
  if (!blob || blob.size >= file.size) {
    return file;
  }

  const ext = blob.type === 'image/webp' ? 'webp' : blob.type === 'image/png' ? 'png' : 'jpg';
  const base = file.name.replace(/\.[^.]+$/, '') || 'imagen';
  return new File([blob], `${base}.${ext}`, { type: blob.type, lastModified: Date.now() });
}

type Drawable = ImageBitmap | HTMLImageElement;

async function loadImage(file: File): Promise<Drawable> {
  if (typeof createImageBitmap === 'function') {
    try {
      return await createImageBitmap(file, { imageOrientation: 'from-image' });
    } catch {
      // Older Safari rejects the options bag; fall through to <img>.
    }
  }
  const img = new Image();
  img.decoding = 'async';
  img.src = URL.createObjectURL(file);
  try {
    await img.decode();
  } catch (err) {
    URL.revokeObjectURL(img.src);
    throw err;
  }
  return img;
}

function release(image: Drawable): void {
  if ('close' in image) {
    image.close();
  } else if (image.src.startsWith('blob:')) {
    URL.revokeObjectURL(image.src);
  }
}

function toBlob(canvas: HTMLCanvasElement, type: string): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, QUALITY));
}
