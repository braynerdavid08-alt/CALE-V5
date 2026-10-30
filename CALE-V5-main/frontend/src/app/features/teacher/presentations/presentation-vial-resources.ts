import { newClientId, SlideElement } from './presentation.models';
import { buildTrafficSignElements, TRAFFIC_SIGN_OPTIONS, TrafficSignOption } from './presentation-traffic-signs';

export type VialResourceCategory =
  | TrafficSignOption['category']
  | 'actores'
  | 'escena';

export interface VialResourceOption {
  key: string;
  label: string;
  category: VialResourceCategory;
  hint?: string;
}

/** Extended educational pack for CEA classes (shape+text composites). */
export const VIAL_RESOURCE_OPTIONS: VialResourceOption[] = [
  ...TRAFFIC_SIGN_OPTIONS,
  { key: 'vel-30', label: 'Velocidad máx. 30', category: 'reglamentacion' },
  { key: 'vel-80', label: 'Velocidad máx. 80', category: 'reglamentacion' },
  { key: 'prohibido-adelantar', label: 'Prohibido adelantar', category: 'reglamentacion' },
  { key: 'solo-buses', label: 'Solo buses', category: 'reglamentacion' },
  { key: 'ninos', label: 'Zona escolar', category: 'prevencion' },
  { key: 'animales', label: 'Paso de animales', category: 'prevencion' },
  { key: 'obra', label: 'Obras en la vía', category: 'prevencion' },
  { key: 'deslizamiento', label: 'Deslizamiento', category: 'prevencion' },
  { key: 'aeropuerto', label: 'Aeropuerto', category: 'informacion' },
  { key: 'restaurante', label: 'Restaurante', category: 'informacion' },
  { key: 'semaforo', label: 'Semáforo', category: 'actores', hint: 'Rojo · amarillo · verde' },
  { key: 'auto', label: 'Automóvil', category: 'actores' },
  { key: 'moto', label: 'Motocicleta', category: 'actores' },
  { key: 'bus', label: 'Bus / servicio público', category: 'actores' },
  { key: 'peaton', label: 'Peatón', category: 'actores' },
  { key: 'ciclista', label: 'Ciclista', category: 'actores' },
  { key: 'cono', label: 'Cono / obra', category: 'escena' },
  { key: 'cruce-esquema', label: 'Cruce esquemático', category: 'escena', hint: 'Intersección en cruz' }
];

export const VIAL_CATEGORY_LABELS: Record<VialResourceCategory, string> = {
  reglamentacion: 'Reglamentación',
  prevencion: 'Prevención',
  informacion: 'Información',
  actores: 'Actores viales',
  escena: 'Escenas'
};

function el(
  type: SlideElement['type'],
  x: number,
  y: number,
  w: number,
  h: number,
  z: number,
  props: SlideElement['props'],
  rotation = 0
): SlideElement {
  return { id: newClientId('vial'), type, x, y, w, h, rotation, z, props };
}

function speedSign(originX: number, originY: number, value: string): SlideElement[] {
  return [
    el('shape', originX + 30, originY + 10, 140, 140, 1, {
      shape: 'ellipse',
      fill: '#FFFFFF',
      stroke: '#D32F2F',
      strokeWidth: 10,
      opacity: 1
    }),
    el('text', originX + 50, originY + 48, 100, 64, 2, {
      text: value,
      fontSize: 44,
      fontWeight: 800,
      color: '#0B1F33',
      align: 'center',
      fontFamily: 'Segoe UI, sans-serif'
    })
  ];
}

function infoBoard(originX: number, originY: number, glyph: string): SlideElement[] {
  return [
    el('shape', originX + 20, originY, 160, 140, 1, {
      shape: 'rect',
      fill: '#1565C0',
      stroke: '#FFFFFF',
      strokeWidth: 4,
      opacity: 1
    }),
    el('text', originX + 40, originY + 36, 120, 72, 2, {
      text: glyph,
      fontSize: glyph.length > 2 ? 36 : 48,
      fontWeight: 800,
      color: '#FFFFFF',
      align: 'center',
      fontFamily: 'Segoe UI, sans-serif'
    })
  ];
}

function warnBoard(originX: number, originY: number, glyph: string, caption: string): SlideElement[] {
  return [
    el('shape', originX + 20, originY, 160, 140, 1, {
      shape: 'triangle',
      fill: '#FBC02D',
      stroke: '#0B1F33',
      strokeWidth: 4,
      opacity: 1
    }),
    el('text', originX + 48, originY + 40, 104, 56, 2, {
      text: glyph,
      fontSize: 32,
      fontWeight: 700,
      color: '#0B1F33',
      align: 'center',
      fontFamily: 'Segoe UI, sans-serif'
    }),
    el('text', originX + 10, originY + 152, 180, 28, 3, {
      text: caption,
      fontSize: 13,
      fontWeight: 600,
      color: '#243447',
      align: 'center',
      fontFamily: 'Segoe UI, sans-serif'
    })
  ];
}

/** Builds canvas elements for a vial resource key. */
export function buildVialResourceElements(
  key: string,
  originX = 200,
  originY = 140
): SlideElement[] {
  const fromSigns = buildTrafficSignElements(key, originX, originY);
  if (fromSigns.length) {
    return fromSigns;
  }

  switch (key) {
    case 'vel-30':
      return speedSign(originX, originY, '30');
    case 'vel-80':
      return speedSign(originX, originY, '80');
    case 'prohibido-adelantar':
      return [
        el('shape', originX + 30, originY, 140, 140, 1, {
          shape: 'ellipse',
          fill: '#FFFFFF',
          stroke: '#D32F2F',
          strokeWidth: 10,
          opacity: 1
        }),
        el('text', originX + 48, originY + 48, 104, 48, 2, {
          text: '🚗🚫',
          fontSize: 28,
          fontWeight: 400,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'solo-buses':
      return [
        el('shape', originX + 30, originY, 140, 140, 1, {
          shape: 'ellipse',
          fill: '#1565C0',
          stroke: '#FFFFFF',
          strokeWidth: 6,
          opacity: 1
        }),
        el('text', originX + 48, originY + 48, 104, 48, 2, {
          text: 'BUS',
          fontSize: 28,
          fontWeight: 800,
          color: '#FFFFFF',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'ninos':
      return warnBoard(originX, originY, '🚸', 'Zona escolar');
    case 'animales':
      return warnBoard(originX, originY, '🐄', 'Animales');
    case 'obra':
      return warnBoard(originX, originY, '🚧', 'Obras');
    case 'deslizamiento':
      return warnBoard(originX, originY, '⚠', 'Deslizamiento');
    case 'aeropuerto':
      return infoBoard(originX, originY, '✈');
    case 'restaurante':
      return infoBoard(originX, originY, '🍴');
    case 'semaforo':
      return [
        el('shape', originX + 60, originY, 80, 200, 1, {
          shape: 'rect',
          fill: '#263238',
          stroke: '#90A4AE',
          strokeWidth: 2,
          opacity: 1
        }),
        el('shape', originX + 74, originY + 16, 52, 52, 2, {
          shape: 'ellipse',
          fill: '#E53935',
          stroke: 'transparent',
          strokeWidth: 0,
          opacity: 1
        }),
        el('shape', originX + 74, originY + 74, 52, 52, 3, {
          shape: 'ellipse',
          fill: '#FBC02D',
          stroke: 'transparent',
          strokeWidth: 0,
          opacity: 1
        }),
        el('shape', originX + 74, originY + 132, 52, 52, 4, {
          shape: 'ellipse',
          fill: '#43A047',
          stroke: 'transparent',
          strokeWidth: 0,
          opacity: 1
        })
      ];
    case 'auto':
      return [
        el('shape', originX, originY + 40, 200, 70, 1, {
          shape: 'rect',
          fill: '#2BB0ED',
          stroke: '#0B1F33',
          strokeWidth: 2,
          opacity: 1
        }),
        el('shape', originX + 36, originY + 8, 110, 40, 2, {
          shape: 'rect',
          fill: '#B3E5FC',
          stroke: '#0B1F33',
          strokeWidth: 2,
          opacity: 1
        }),
        el('shape', originX + 24, originY + 96, 36, 36, 3, {
          shape: 'ellipse',
          fill: '#37474F',
          stroke: '#90A4AE',
          strokeWidth: 2,
          opacity: 1
        }),
        el('shape', originX + 140, originY + 96, 36, 36, 4, {
          shape: 'ellipse',
          fill: '#37474F',
          stroke: '#90A4AE',
          strokeWidth: 2,
          opacity: 1
        }),
        el('text', originX, originY + 140, 200, 28, 5, {
          text: 'Automóvil',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'moto':
      return [
        el('shape', originX + 50, originY + 36, 100, 36, 1, {
          shape: 'rect',
          fill: '#FF7043',
          stroke: '#0B1F33',
          strokeWidth: 2,
          opacity: 1
        }),
        el('shape', originX + 30, originY + 70, 40, 40, 2, {
          shape: 'ellipse',
          fill: '#37474F',
          stroke: '#90A4AE',
          strokeWidth: 2,
          opacity: 1
        }),
        el('shape', originX + 130, originY + 70, 40, 40, 3, {
          shape: 'ellipse',
          fill: '#37474F',
          stroke: '#90A4AE',
          strokeWidth: 2,
          opacity: 1
        }),
        el('text', originX + 20, originY + 8, 160, 28, 4, {
          text: '🏍',
          fontSize: 28,
          fontWeight: 400,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        }),
        el('text', originX, originY + 120, 200, 28, 5, {
          text: 'Motocicleta',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'bus':
      return [
        el('shape', originX, originY + 20, 220, 90, 1, {
          shape: 'rect',
          fill: '#FDD835',
          stroke: '#0B1F33',
          strokeWidth: 2,
          opacity: 1
        }),
        el('text', originX + 60, originY + 42, 100, 40, 2, {
          text: 'BUS',
          fontSize: 28,
          fontWeight: 800,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        }),
        el('shape', originX + 28, originY + 100, 36, 36, 3, {
          shape: 'ellipse',
          fill: '#37474F',
          stroke: '#90A4AE',
          strokeWidth: 2,
          opacity: 1
        }),
        el('shape', originX + 156, originY + 100, 36, 36, 4, {
          shape: 'ellipse',
          fill: '#37474F',
          stroke: '#90A4AE',
          strokeWidth: 2,
          opacity: 1
        })
      ];
    case 'peaton':
      return [
        el('text', originX + 40, originY, 120, 140, 1, {
          text: '🚶',
          fontSize: 88,
          fontWeight: 400,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        }),
        el('text', originX, originY + 140, 200, 28, 2, {
          text: 'Peatón',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'ciclista':
      return [
        el('text', originX + 40, originY, 120, 140, 1, {
          text: '🚴',
          fontSize: 88,
          fontWeight: 400,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        }),
        el('text', originX, originY + 140, 200, 28, 2, {
          text: 'Ciclista',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'cono':
      return [
        el('shape', originX + 40, originY + 20, 120, 140, 1, {
          shape: 'triangle',
          fill: '#FF6D00',
          stroke: '#FFFFFF',
          strokeWidth: 4,
          opacity: 1
        }),
        el('text', originX, originY + 170, 200, 28, 2, {
          text: 'Cono / obra',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'cruce-esquema':
      return [
        el('shape', originX, originY + 70, 240, 60, 1, {
          shape: 'rect',
          fill: '#607D8B',
          stroke: 'transparent',
          strokeWidth: 0,
          opacity: 1
        }),
        el('shape', originX + 90, originY, 60, 200, 2, {
          shape: 'rect',
          fill: '#607D8B',
          stroke: 'transparent',
          strokeWidth: 0,
          opacity: 1
        }),
        el('shape', originX + 20, originY + 92, 40, 12, 3, {
          shape: 'rect',
          fill: '#FFFFFF',
          stroke: 'transparent',
          strokeWidth: 0,
          opacity: 0.9
        }),
        el('shape', originX + 180, originY + 92, 40, 12, 4, {
          shape: 'rect',
          fill: '#FFFFFF',
          stroke: 'transparent',
          strokeWidth: 0,
          opacity: 0.9
        }),
        el('text', originX, originY + 210, 240, 28, 5, {
          text: 'Intersección',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    default:
      return [];
  }
}
