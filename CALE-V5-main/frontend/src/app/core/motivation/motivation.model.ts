export type MotivationAudience = 'all' | 'Student' | 'Teacher' | 'School' | 'Admin';

export type MotivationCategory =
  | 'cuidado'
  | 'atencion'
  | 'anticipacion'
  | 'respeto'
  | 'formacion';

export type MotivationMoment = 'any' | 'morning' | 'afternoon' | 'night';

export interface MotivationTip {
  id: string;
  /** Frase corta y memorable (barra superior). */
  headline: string;
  /** Consejo práctico accionable. */
  detail: string;
  category: MotivationCategory;
  audience: MotivationAudience;
  moment: MotivationMoment;
}

export const MOTIVATION_CATEGORY_LABEL: Record<MotivationCategory, string> = {
  cuidado: 'Autocuidado',
  atencion: 'Atención',
  anticipacion: 'Anticipación',
  respeto: 'Respeto vial',
  formacion: 'Formación'
};
