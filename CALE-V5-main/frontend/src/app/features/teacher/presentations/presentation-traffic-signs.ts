import { newClientId, SlideElement } from './presentation.models';

export interface TrafficSignOption {
  key: string;
  label: string;
  category: 'reglamentacion' | 'prevencion' | 'informacion';
}

export const TRAFFIC_SIGN_OPTIONS: TrafficSignOption[] = [
  { key: 'pare', label: 'PARE', category: 'reglamentacion' },
  { key: 'ceda', label: 'CEDA EL PASO', category: 'reglamentacion' },
  { key: 'vel-60', label: 'Velocidad máx. 60', category: 'reglamentacion' },
  { key: 'prohibido-estacionar', label: 'Prohibido estacionar', category: 'reglamentacion' },
  { key: 'sentido-obligatorio', label: 'Sentido obligatorio', category: 'reglamentacion' },
  { key: 'curva', label: 'Curva peligrosa', category: 'prevencion' },
  { key: 'cruce-peatones', label: 'Cruce de peatones', category: 'prevencion' },
  { key: 'resalto', label: 'Resalto / badén', category: 'prevencion' },
  { key: 'interseccion', label: 'Intersección', category: 'prevencion' },
  { key: 'hospital', label: 'Hospital', category: 'informacion' },
  { key: 'estacionamiento', label: 'Estacionamiento', category: 'informacion' },
  { key: 'gasolinera', label: 'Gasolinera', category: 'informacion' }
];

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
  return { id: newClientId('sign'), type, x, y, w, h, rotation, z, props };
}

/** Inserts a traffic-sign group centered around (originX, originY). */
export function buildTrafficSignElements(
  key: string,
  originX = 200,
  originY = 140
): SlideElement[] {
  switch (key) {
    case 'pare':
      return [
        el('shape', originX, originY, 200, 200, 1, {
          shape: 'octagon',
          fill: '#D32F2F',
          stroke: '#FFFFFF',
          strokeWidth: 8,
          opacity: 1
        }),
        el('text', originX + 20, originY + 68, 160, 64, 2, {
          text: 'PARE',
          fontSize: 36,
          fontWeight: 800,
          color: '#FFFFFF',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'ceda':
      return [
        el('shape', originX + 20, originY, 160, 140, 1, {
          shape: 'triangle',
          fill: '#FFFFFF',
          stroke: '#D32F2F',
          strokeWidth: 6,
          opacity: 1
        }),
        el('text', originX + 36, originY + 52, 128, 48, 2, {
          text: 'CEDA\nEL PASO',
          fontSize: 18,
          fontWeight: 800,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'vel-60':
      return [
        el('shape', originX + 30, originY + 10, 140, 140, 1, {
          shape: 'ellipse',
          fill: '#FFFFFF',
          stroke: '#D32F2F',
          strokeWidth: 10,
          opacity: 1
        }),
        el('text', originX + 50, originY + 48, 100, 64, 2, {
          text: '60',
          fontSize: 44,
          fontWeight: 800,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        }),
        el('text', originX + 30, originY + 168, 140, 28, 3, {
          text: 'Velocidad máxima',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'prohibido-estacionar':
      return [
        el('shape', originX + 30, originY, 140, 140, 1, {
          shape: 'ellipse',
          fill: '#FFFFFF',
          stroke: '#D32F2F',
          strokeWidth: 10,
          opacity: 1
        }),
        el('shape', originX + 58, originY + 28, 84, 84, 2, {
          shape: 'rect',
          fill: '#D32F2F',
          stroke: 'transparent',
          strokeWidth: 0,
          opacity: 1
        }),
        el('text', originX + 72, originY + 48, 56, 48, 3, {
          text: 'E',
          fontSize: 36,
          fontWeight: 800,
          color: '#FFFFFF',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'sentido-obligatorio':
      return [
        el('shape', originX + 30, originY, 140, 140, 1, {
          shape: 'ellipse',
          fill: '#1565C0',
          stroke: '#FFFFFF',
          strokeWidth: 6,
          opacity: 1
        }),
        el('text', originX + 78, originY + 36, 44, 72, 2, {
          text: '→',
          fontSize: 48,
          fontWeight: 700,
          color: '#FFFFFF',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'curva':
      return [
        el('shape', originX + 20, originY, 160, 140, 1, {
          shape: 'triangle',
          fill: '#FBC02D',
          stroke: '#0B1F33',
          strokeWidth: 4,
          opacity: 1
        }),
        el('text', originX + 52, originY + 44, 96, 48, 2, {
          text: '↷',
          fontSize: 40,
          fontWeight: 700,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        }),
        el('text', originX + 20, originY + 152, 160, 28, 3, {
          text: 'Curva peligrosa',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'cruce-peatones':
      return [
        el('shape', originX + 20, originY, 160, 140, 1, {
          shape: 'rect',
          fill: '#FBC02D',
          stroke: '#0B1F33',
          strokeWidth: 4,
          opacity: 1
        }),
        el('text', originX + 52, originY + 36, 96, 72, 2, {
          text: '🚶',
          fontSize: 40,
          fontWeight: 400,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        }),
        el('text', originX + 20, originY + 152, 160, 28, 3, {
          text: 'Cruce peatonal',
          fontSize: 14,
          fontWeight: 600,
          color: '#243447',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'resalto':
      return [
        el('shape', originX + 20, originY, 160, 140, 1, {
          shape: 'triangle',
          fill: '#FBC02D',
          stroke: '#0B1F33',
          strokeWidth: 4,
          opacity: 1
        }),
        el('text', originX + 52, originY + 40, 96, 56, 2, {
          text: '∧∧',
          fontSize: 28,
          fontWeight: 800,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'interseccion':
      return [
        el('shape', originX + 20, originY, 160, 140, 1, {
          shape: 'triangle',
          fill: '#FBC02D',
          stroke: '#0B1F33',
          strokeWidth: 4,
          opacity: 1
        }),
        el('text', originX + 52, originY + 40, 96, 56, 2, {
          text: '✚',
          fontSize: 36,
          fontWeight: 700,
          color: '#0B1F33',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'hospital':
      return [
        el('shape', originX + 20, originY, 160, 140, 1, {
          shape: 'rect',
          fill: '#1565C0',
          stroke: '#FFFFFF',
          strokeWidth: 4,
          opacity: 1
        }),
        el('text', originX + 52, originY + 36, 96, 72, 2, {
          text: 'H',
          fontSize: 48,
          fontWeight: 800,
          color: '#FFFFFF',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'estacionamiento':
      return [
        el('shape', originX + 20, originY, 160, 140, 1, {
          shape: 'rect',
          fill: '#1565C0',
          stroke: '#FFFFFF',
          strokeWidth: 4,
          opacity: 1
        }),
        el('text', originX + 52, originY + 36, 96, 72, 2, {
          text: 'P',
          fontSize: 48,
          fontWeight: 800,
          color: '#FFFFFF',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    case 'gasolinera':
      return [
        el('shape', originX + 20, originY, 160, 140, 1, {
          shape: 'rect',
          fill: '#1565C0',
          stroke: '#FFFFFF',
          strokeWidth: 4,
          opacity: 1
        }),
        el('text', originX + 52, originY + 36, 96, 72, 2, {
          text: '⛽',
          fontSize: 40,
          fontWeight: 400,
          color: '#FFFFFF',
          align: 'center',
          fontFamily: 'Segoe UI, sans-serif'
        })
      ];
    default:
      return [];
  }
}
