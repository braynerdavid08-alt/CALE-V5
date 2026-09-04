export type SlideElementType = 'text' | 'image' | 'video' | 'shape' | 'line' | 'arrow';

export type ShapeKind = 'rect' | 'ellipse' | 'triangle' | 'octagon';

export interface SlideBackground {
  type: 'solid' | 'gradient' | 'image';
  color: string;
  color2?: string;
  imageUrl?: string;
}

export interface TextProps {
  text: string;
  fontSize: number;
  fontWeight: number;
  color: string;
  align: 'left' | 'center' | 'right';
  fontFamily: string;
  italic?: boolean;
  underline?: boolean;
}

export interface ImageCrop {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface ImageProps {
  src: string;
  opacity?: number;
  objectFit?: 'contain' | 'cover';
  crop?: ImageCrop;
}

export interface VideoProps {
  src: string;
  autoplay?: boolean;
  loop?: boolean;
  muted?: boolean;
}

export interface ShapeProps {
  shape: ShapeKind;
  fill: string;
  stroke: string;
  strokeWidth: number;
  opacity: number;
}

export interface LineProps {
  color: string;
  strokeWidth: number;
  arrowEnd?: boolean;
}

export interface SlideElement {
  id: string;
  type: SlideElementType;
  x: number;
  y: number;
  w: number;
  h: number;
  rotation: number;
  z: number;
  /** Canvas group (not classroom GroupId). Shared id = same group. */
  groupId?: string | null;
  props: TextProps | ImageProps | VideoProps | ShapeProps | LineProps;
}

export interface EditorSlide {
  clientId: string;
  id?: number;
  title: string;
  notes: string;
  background: SlideBackground;
  elements: SlideElement[];
}

export interface PresentationListItem {
  id: number;
  title: string;
  description?: string | null;
  category: string;
  groupId?: number | null;
  thumbnailUrl?: string | null;
  slideCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface PresentationSummary {
  total: number;
  latest: PresentationListItem | null;
}

export interface PresentationSlideDto {
  id: number;
  position: number;
  title: string;
  notes?: string | null;
  backgroundJson: string;
  elementsJson: string;
}

export interface PresentationDetail {
  id: number;
  title: string;
  description?: string | null;
  category: string;
  groupId?: number | null;
  schoolId?: number | null;
  thumbnailUrl?: string | null;
  slideCount: number;
  createdAt: string;
  updatedAt: string;
  updatedByUserId: number;
  slides: PresentationSlideDto[];
}

export const PRESENTATION_IMPORT_MAX_BYTES = 200 * 1024 * 1024;
export const PRESENTATION_MEDIA_MAX_BYTES = 100 * 1024 * 1024;
export const SLIDE_W = 960;
export const SLIDE_H = 540;

export const PRESENTATION_CATEGORIES = [
  'Normas de tránsito',
  'Señales de tránsito',
  'Seguridad vial',
  'Conducción defensiva',
  'Mecánica básica',
  'Primeros auxilios',
  'Educación vial',
  'Preparación para examen',
  'Otro'
] as const;

export const TEMPLATE_OPTIONS: { key: string; label: string }[] = [
  { key: 'blank', label: 'En blanco' },
  { key: 'cover', label: 'Portada Mi CALE' },
  { key: 'title-content', label: 'Título + contenido' },
  { key: 'signal', label: 'Señal de tránsito' },
  { key: 'case', label: 'Caso práctico' },
  { key: 'quiz', label: 'Evaluación rápida' },
  { key: 'compare', label: 'Comparación' },
  { key: 'summary', label: 'Resumen' },
  { key: 'closing', label: 'Cierre de clase' }
];

export function newClientId(prefix = 'id'): string {
  return `${prefix}-${Math.random().toString(36).slice(2, 10)}`;
}

export function parseBackground(json: string): SlideBackground {
  try {
    const v = JSON.parse(json) as SlideBackground;
    return {
      type: v.type || 'solid',
      color: v.color || '#ffffff',
      color2: v.color2,
      imageUrl: v.imageUrl
    };
  } catch {
    return { type: 'solid', color: '#ffffff' };
  }
}

export function parseElements(json: string): SlideElement[] {
  try {
    const arr = JSON.parse(json) as SlideElement[];
    return Array.isArray(arr) ? arr : [];
  } catch {
    return [];
  }
}

export function isFullBleedImage(el: SlideElement): boolean {
  if (el.type !== 'image') {
    return false;
  }
  const coversMost = el.w >= SLIDE_W * 0.82 && el.h >= SLIDE_H * 0.82;
  const pinnedNearOrigin = el.x <= SLIDE_W * 0.12 && el.y <= SLIDE_H * 0.12;
  return coversMost && pinnedNearOrigin;
}

export function imageSrc(el: SlideElement): string | null {
  if (el.type !== 'image') {
    return null;
  }
  const src = (el.props as ImageProps).src?.trim();
  return src || null;
}

function isImportStubText(el: SlideElement): boolean {
  if (el.type !== 'text') {
    return false;
  }
  const text = ((el.props as TextProps).text ?? '').trim();
  const atTitleSlot = el.x === 64 && el.y === 48 && el.w === 832;
  const atBodySlot = el.x === 64 && el.y === 140 && el.w === 832 && el.h >= 280;
  // Solo quitar placeholders vacíos o genéricos — nunca texto real del PPT.
  if (!text) {
    return atTitleSlot || atBodySlot;
  }
  if (atTitleSlot && /^Diapositiva\s+\d+$/i.test(text)) {
    return true;
  }
  if (atBodySlot && text.length < 3) {
    return true;
  }
  return false;
}

/**
 * Si una foto cubre casi toda la diapositiva, pásala a fondo para que el texto
 * y las formas del PowerPoint queden encima y se puedan editar / conservar el orden.
 */
export function unlockImportedPhotoSlide(slide: EditorSlide): EditorSlide {
  const photos = slide.elements.filter(isFullBleedImage);
  const topPhoto = photos.length
    ? [...photos].sort((a, b) => a.z - b.z)[0]
    : null;
  const src = topPhoto ? imageSrc(topPhoto) : null;
  const background: SlideBackground = src
    ? { type: 'image', color: slide.background.color || '#F7F9FC', imageUrl: src }
    : slide.background;
  const remaining = slide.elements.filter((el) => el.id !== topPhoto?.id);
  const elements = remaining.filter((el) => !isImportStubText(el));
  if (
    background.type === slide.background.type
    && background.imageUrl === slide.background.imageUrl
    && elements.length === slide.elements.length
  ) {
    return slide;
  }
  return { ...slide, background, elements };
}

export function dtoToEditorSlides(detail: PresentationDetail): EditorSlide[] {
  return detail.slides
    .slice()
    .sort((a, b) => a.position - b.position)
    .map((s) =>
      unlockImportedPhotoSlide({
        clientId: newClientId('slide'),
        id: s.id,
        title: s.title,
        notes: s.notes || '',
        background: parseBackground(s.backgroundJson),
        elements: parseElements(s.elementsJson)
      })
    );
}

export const DEFAULT_IMAGE_CROP: ImageCrop = { x: 0, y: 0, w: 1, h: 1 };

export function normalizeImageCrop(crop?: ImageCrop): ImageCrop {
  if (!crop) {
    return { ...DEFAULT_IMAGE_CROP };
  }

  const w = Math.min(1, Math.max(0.05, crop.w));
  const h = Math.min(1, Math.max(0.05, crop.h));
  const x = Math.min(1 - w, Math.max(0, crop.x));
  const y = Math.min(1 - h, Math.max(0, crop.y));
  return { x, y, w, h };
}

export function hasImageCrop(crop?: ImageCrop): boolean {
  const c = normalizeImageCrop(crop);
  return c.x > 0.001 || c.y > 0.001 || c.w < 0.999 || c.h < 0.999;
}

export function imageElementStyles(props: ImageProps): Record<string, string> {
  const crop = normalizeImageCrop(props.crop);
  if (hasImageCrop(props.crop)) {
    return {
      width: `${100 / crop.w}%`,
      height: `${100 / crop.h}%`,
      marginLeft: `${(-100 * crop.x) / crop.w}%`,
      marginTop: `${(-100 * crop.y) / crop.h}%`,
      objectFit: 'fill'
    };
  }

  return {
    objectFit: props.objectFit ?? 'contain'
  };
}

export function backgroundCss(bg: SlideBackground): Record<string, string> {
  if (bg.type === 'gradient') {
    return {
      background: `linear-gradient(135deg, ${bg.color}, ${bg.color2 || '#2BB0ED'})`
    };
  }
  if (bg.type === 'image' && bg.imageUrl) {
    return {
      backgroundImage: `url(${bg.imageUrl})`,
      backgroundSize: 'cover',
      backgroundPosition: 'center'
    };
  }
  return { background: bg.color || '#ffffff' };
}
