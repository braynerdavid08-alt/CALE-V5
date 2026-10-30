import { MASCOT_NAME, TourStep } from './tour.types';

/**
 * Compact student tour (v3): fewer steps so mobile (esp. iPhone) finishes faster
 * and spends less time waiting on targets.
 */
export const STUDENT_TOUR_STEPS: TourStep[] = [
  {
    id: 'panel',
    route: '/student',
    target: '[data-tour="student-panel"]',
    title: 'Tu panel',
    body: `Aquí empieza tu día: progreso, racha y accesos. Soy ${MASCOT_NAME} y te acompaño.`
  },
  {
    id: 'learn',
    route: '/student',
    target: '[data-tour="student-courses"]',
    title: 'Aprende a tu ritmo',
    body: 'Cursos virtuales con videos y actividades. Cerca tienes el reto diario y el simulacro del examen.'
  },
  {
    id: 'live',
    route: '/student',
    target: '[data-tour="student-live"]',
    title: 'Clase con tu instructor',
    body: 'Entra al aula en vivo con código o QR. En Mis clases y la Biblioteca ves materiales y normas.'
  },
  {
    id: 'kazu',
    route: '/student',
    target: '[data-tour="student-kazu"]',
    title: `Habla con ${MASCOT_NAME}`,
    body: `${MASCOT_NAME} responde dudas de horarios y formación. Siempre te pedirá confirmar antes de hacer cambios.`
  }
];
