import { MASCOT_NAME, TourStep } from './tour.types';

export const STUDENT_TOUR_STEPS: TourStep[] = [
  {
    id: 'panel',
    route: '/student',
    target: '[data-tour="student-panel"]',
    title: 'Tu panel',
    body: `Aquí empieza tu día. Verás tu progreso, racha y accesos rápidos. Soy ${MASCOT_NAME} y te acompaño en esta guía.`
  },
  {
    id: 'daily',
    route: '/student',
    target: '[data-tour="student-daily"]',
    title: 'Reto diario',
    body: 'Practica un poco cada día para mantener tu racha. El simulacro también está cerca si quieres un examen completo.'
  },
  {
    id: 'courses',
    route: '/student',
    target: '[data-tour="student-courses"]',
    title: 'Cursos virtuales',
    body: 'Aquí están las lecciones con videos y actividades. Es tu formación teórica paso a paso, a tu ritmo.'
  },
  {
    id: 'live',
    route: '/student',
    target: '[data-tour="student-live"]',
    title: 'Aula en vivo',
    body: 'Cuando tu instructor abre una clase, entras aquí con el código o el QR para participar en tiempo real.'
  },
  {
    id: 'classes-library',
    route: '/student',
    target: '[data-tour="student-classes"]',
    title: 'Mis clases y Biblioteca',
    body: 'En Mis clases ves materiales de tu CEA. Justo debajo, la Biblioteca jurídica reúne las normas de tránsito para consultar cuando estudies.'
  },
  {
    id: 'kazu',
    route: '/student',
    target: '[data-tour="student-kazu"]',
    title: `Habla con ${MASCOT_NAME}`,
    body: `${MASCOT_NAME} es el asistente de Luz Verde. Pregúntale por horas, agenda o dudas de tu formación. Siempre te pedirá confirmar antes de hacer cambios.`
  }
];
