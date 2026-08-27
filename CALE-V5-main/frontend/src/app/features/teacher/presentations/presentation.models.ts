export type SlideElementType = 'text' | 'image' | 'shape' | 'line' | 'arrow';

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

export interface ImageProps {
  src: string;
  opacity?: number;
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
  props: TextProps | ImageProps | ShapeProps | LineProps;
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

export function dtoToEditorSlides(detail: PresentationDetail): EditorSlide[] {
  return detail.slides
    .slice()
    .sort((a, b) => a.position - b.position)
    .map((s) => ({
      clientId: newClientId('slide'),
      id: s.id,
      title: s.title,
      notes: s.notes || '',
      background: parseBackground(s.backgroundJson),
      elements: parseElements(s.elementsJson)
    }));
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
