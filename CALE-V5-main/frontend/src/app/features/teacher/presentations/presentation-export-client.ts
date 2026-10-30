import { resolveMediaUrl } from '../../../core/media/resolve-media-url';
import {
  EditorSlide,
  ImageProps,
  LineProps,
  QuestionProps,
  SLIDE_H,
  SLIDE_W,
  ShapeProps,
  SlideElement,
  TextProps,
  VideoProps,
  backgroundCss,
  shapeClipPath
} from './presentation.models';

/** Shown next to Office export options so instructors know what is lost. */
export const OFFICE_EXPORT_LIMITS =
  'PowerPoint conserva texto, imágenes, formas y preguntas (como texto). Los videos van como marcador con enlace. Excel/Word solo llevan el texto (sin diseño).';

export const OFFICE_IMPORT_LIMITS =
  'PowerPoint: texto, formas, imágenes y videos embebidos. El texto dentro de una foto no se edita. Excel/Word solo crean un esquema de títulos y párrafos (sin diseño ni medios). Máximo 200 MB.';

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function visibleElements(slide: EditorSlide): SlideElement[] {
  return [...slide.elements]
    .filter((e) => !e.hidden)
    .sort((a, b) => a.z - b.z);
}

function elementHtml(el: SlideElement): string {
  const box = `position:absolute;left:${el.x}px;top:${el.y}px;width:${el.w}px;height:${el.h}px;transform:rotate(${el.rotation}deg);overflow:hidden;`;
  if (el.type === 'text') {
    const p = el.props as TextProps;
    const style =
      `${box}font-size:${p.fontSize}px;font-weight:${p.fontWeight};color:${p.color};` +
      `text-align:${p.align};font-family:${p.fontFamily};` +
      `font-style:${p.italic ? 'italic' : 'normal'};` +
      `text-decoration:${p.underline ? 'underline' : 'none'};white-space:pre-wrap;`;
    return `<div style="${style}">${escapeHtml(p.text || '')}</div>`;
  }
  if (el.type === 'image') {
    const p = el.props as ImageProps;
    const src = resolveMediaUrl(p.src);
    return `<div style="${box}"><img src="${escapeHtml(src)}" style="width:100%;height:100%;object-fit:${p.objectFit || 'contain'};opacity:${p.opacity ?? 1};" /></div>`;
  }
  if (el.type === 'video') {
    const p = el.props as VideoProps;
    return `<div style="${box}background:#111;color:#fff;display:grid;place-items:center;font:600 28px Segoe UI,sans-serif;">Video</div>`;
  }
  if (el.type === 'shape') {
    const p = el.props as ShapeProps;
    const clip = shapeClipPath(p.shape);
    const clipCss = clip ? `clip-path:${clip};` : '';
    const radius = p.shape === 'ellipse' ? '50%' : '4px';
    return `<div style="${box}${clipCss}background:${p.fill};border:${p.strokeWidth}px solid ${p.stroke};opacity:${p.opacity};border-radius:${radius};"></div>`;
  }
  if (el.type === 'line' || el.type === 'arrow') {
    const p = el.props as LineProps;
    return `<div style="${box}display:flex;align-items:center;"><div style="flex:1;height:${p.strokeWidth}px;background:${p.color};"></div></div>`;
  }
  if (el.type === 'question') {
    const p = el.props as QuestionProps;
    const opts = (p.options || [])
      .map((o, i) => `<li>${escapeHtml(String.fromCharCode(65 + i))}. ${escapeHtml(o)}</li>`)
      .join('');
    const img = p.imageUrl
      ? `<img src="${escapeHtml(resolveMediaUrl(p.imageUrl))}" alt="" style="max-width:100%;max-height:220px;object-fit:contain;display:block;margin:0 auto 12px;border-radius:12px;" />`
      : '';
    return `<div style="${box}background:#0f2740;color:#fff;padding:24px;box-sizing:border-box;border-radius:16px;">
      <div style="font-size:18px;opacity:.8;margin-bottom:8px;">Pregunta en vivo</div>
      ${img}
      <div style="font-size:28px;font-weight:700;margin-bottom:12px;">${escapeHtml(p.text || '')}</div>
      <ul style="font-size:22px;margin:0;padding-left:1.2em;">${opts}</ul>
    </div>`;
  }
  return '';
}

function slidePageHtml(slide: EditorSlide, index: number): string {
  const bg = backgroundCss(slide.background);
  const bgParts = Object.entries(bg)
    .map(([k, v]) => {
      const cssKey = k.replace(/[A-Z]/g, (m) => `-${m.toLowerCase()}`);
      let value = v;
      if (k === 'backgroundImage' && slide.background.imageUrl) {
        value = `url(${resolveMediaUrl(slide.background.imageUrl)})`;
      }
      return `${cssKey}:${value}`;
    })
    .join(';');
  const els = visibleElements(slide).map(elementHtml).join('');
  return `<section class="page" data-slide="${index + 1}">
    <div class="slide" style="${bgParts}">${els}</div>
    <p class="caption">${index + 1}. ${escapeHtml(slide.title || 'Sin título')}</p>
  </section>`;
}

/** Opens a print window so the instructor can save as PDF (no new npm deps). */
export function openPresentationPrintPdf(title: string, slides: EditorSlide[]): void {
  const pages = slides.map((s, i) => slidePageHtml(s, i)).join('');
  const html = `<!DOCTYPE html><html lang="es"><head><meta charset="utf-8" />
<title>${escapeHtml(title || 'Presentación')} — Luz Verde</title>
<style>
  @page { size: landscape; margin: 10mm; }
  body { margin: 0; font-family: "Segoe UI", system-ui, sans-serif; color: #0b1f33; background: #fff; }
  h1 { font-size: 18px; margin: 0 0 12px; }
  .page { break-after: page; page-break-after: always; margin-bottom: 16px; }
  .page:last-child { break-after: auto; page-break-after: auto; }
  .slide {
    position: relative; width: ${SLIDE_W}px; height: ${SLIDE_H}px;
    transform-origin: top left; transform: scale(0.48);
    margin-bottom: calc(${SLIDE_H}px * -0.52); overflow: hidden; border: 1px solid #d0d7de;
  }
  .caption { font-size: 12px; color: #5b6b7c; margin: 8px 0 0; }
  @media print {
    .slide { transform: scale(0.55); margin-bottom: calc(${SLIDE_H}px * -0.45); border: none; }
    .no-print { display: none !important; }
  }
</style></head><body>
  <p class="no-print" style="padding:12px;background:#e8f4fc;">
    Usa <strong>Imprimir → Guardar como PDF</strong>. Orientación horizontal recomendada.
  </p>
  <h1>${escapeHtml(title || 'Presentación')}</h1>
  ${pages}
  <script>window.onload=()=>setTimeout(()=>window.print(),250);</script>
</body></html>`;

  const win = window.open('', '_blank');
  if (!win) {
    throw new Error('El navegador bloqueó la ventana de impresión. Permite ventanas emergentes e inténtalo de nuevo.');
  }
  win.document.open();
  win.document.write(html);
  win.document.close();
}

function loadImage(src: string): Promise<HTMLImageElement | null> {
  return new Promise((resolve) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src;
  });
}

/** Raster PNG of one slide (best-effort; remote images may fail CORS). */
export async function renderSlideToPngBlob(slide: EditorSlide): Promise<Blob> {
  const canvas = document.createElement('canvas');
  canvas.width = SLIDE_W;
  canvas.height = SLIDE_H;
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    throw new Error('No se pudo crear el lienzo de exportación.');
  }

  const bg = slide.background;
  if (bg.type === 'image' && bg.imageUrl) {
    const img = await loadImage(resolveMediaUrl(bg.imageUrl));
    if (img) {
      ctx.drawImage(img, 0, 0, SLIDE_W, SLIDE_H);
    } else {
      ctx.fillStyle = bg.color || '#0B1F33';
      ctx.fillRect(0, 0, SLIDE_W, SLIDE_H);
    }
  } else if (bg.type === 'gradient' && bg.color2) {
    const g = ctx.createLinearGradient(0, 0, SLIDE_W, SLIDE_H);
    g.addColorStop(0, bg.color || '#0B1F33');
    g.addColorStop(1, bg.color2);
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, SLIDE_W, SLIDE_H);
  } else {
    ctx.fillStyle = bg.color || '#ffffff';
    ctx.fillRect(0, 0, SLIDE_W, SLIDE_H);
  }

  for (const el of visibleElements(slide)) {
    ctx.save();
    ctx.translate(el.x + el.w / 2, el.y + el.h / 2);
    ctx.rotate((el.rotation * Math.PI) / 180);
    ctx.translate(-el.w / 2, -el.h / 2);

    if (el.type === 'text') {
      const p = el.props as TextProps;
      ctx.fillStyle = p.color || '#000';
      ctx.font = `${p.italic ? 'italic ' : ''}${p.fontWeight || 400} ${p.fontSize || 20}px ${p.fontFamily || 'Segoe UI, sans-serif'}`;
      ctx.textAlign = p.align === 'center' ? 'center' : p.align === 'right' ? 'right' : 'left';
      const x = p.align === 'center' ? el.w / 2 : p.align === 'right' ? el.w : 0;
      const lines = (p.text || '').split('\n');
      const lineH = (p.fontSize || 20) * 1.25;
      lines.forEach((line, i) => {
        ctx.fillText(line, x, (p.fontSize || 20) + i * lineH);
      });
    } else if (el.type === 'image') {
      const p = el.props as ImageProps;
      const img = await loadImage(resolveMediaUrl(p.src));
      if (img) {
        ctx.globalAlpha = p.opacity ?? 1;
        ctx.drawImage(img, 0, 0, el.w, el.h);
      }
    } else if (el.type === 'shape') {
      const p = el.props as ShapeProps;
      ctx.globalAlpha = p.opacity ?? 1;
      ctx.fillStyle = p.fill || '#2BB0ED';
      ctx.strokeStyle = p.stroke || 'transparent';
      ctx.lineWidth = p.strokeWidth || 0;
      if (p.shape === 'ellipse') {
        ctx.beginPath();
        ctx.ellipse(el.w / 2, el.h / 2, el.w / 2, el.h / 2, 0, 0, Math.PI * 2);
        ctx.fill();
        if (p.strokeWidth) ctx.stroke();
      } else {
        ctx.fillRect(0, 0, el.w, el.h);
        if (p.strokeWidth) ctx.strokeRect(0, 0, el.w, el.h);
      }
    } else if (el.type === 'line' || el.type === 'arrow') {
      const p = el.props as LineProps;
      ctx.strokeStyle = p.color || '#fff';
      ctx.lineWidth = p.strokeWidth || 4;
      ctx.beginPath();
      ctx.moveTo(0, el.h / 2);
      ctx.lineTo(el.w, el.h / 2);
      ctx.stroke();
    } else if (el.type === 'question') {
      const p = el.props as QuestionProps;
      ctx.fillStyle = '#0f2740';
      ctx.fillRect(0, 0, el.w, el.h);
      ctx.fillStyle = '#fff';
      ctx.font = '700 28px Segoe UI, sans-serif';
      ctx.fillText(p.text || 'Pregunta', 24, 48);
    } else if (el.type === 'video') {
      ctx.fillStyle = '#111';
      ctx.fillRect(0, 0, el.w, el.h);
      ctx.fillStyle = '#fff';
      ctx.font = '600 28px Segoe UI, sans-serif';
      ctx.fillText('Video', 24, 48);
    }
    ctx.restore();
  }

  return await new Promise<Blob>((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (blob) resolve(blob);
      else reject(new Error('No se pudo generar el PNG.'));
    }, 'image/png');
  });
}

export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}
