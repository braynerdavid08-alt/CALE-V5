import { LegalDocKey } from './legal.api';

export const LEGAL_LINKS: ReadonlyArray<{ key: LegalDocKey; label: string }> = [
  { key: 'terms', label: 'Términos y Condiciones' },
  { key: 'privacy', label: 'Aviso de Privacidad' },
  { key: 'data-processing', label: 'Tratamiento de Datos' },
  { key: 'ai-policy', label: 'Uso de IA' },
  { key: 'school-terms', label: 'Condiciones para escuelas' }
];
