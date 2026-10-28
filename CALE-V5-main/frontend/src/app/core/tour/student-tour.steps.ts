import { MASCOT_NAME, TourStep } from './tour.types';

export const STUDENT_TOUR_STEPS: TourStep[] = [
  {
    id: 'panel',
    route: '/student',
    target: '[data-tour="student-panel"]',
    title: 'Tu panel',
    body: `Aquí empieza tu día en Luz Verde. Verás tu progreso, racha y accesos rápidos. ${MASCOT_NAME} te acompaña en esta guía corta.`
  },
  {
    id: 'daily',
    route: '/student',
    target: '[data-tour="student-daily"]',
    title: 'Reto diario',
    body: 'Este es tu reto del día. Sirve para practicar un poco cada jornada y mantener tu racha.'
  },
  {
    id: 'simulator',
    route: '/student',
    target: '[data-tour="student-simulator"]',
    title: 'Simulacro',
    body: 'Con el simulacro practicas preguntas tipo examen CALE, a tu ritmo, todas las veces que quieras.'
  },
  {
    id: 'school',
    route: '/student',
    target: '[data-tour="student-school"]',
    title: 'Tu escuela',
    body: 'Si estudias con un CEA, aquí vinculas tu escuela o entras a tus clases de teoría y manejo.'
  },
  {
    id: 'profile',
    route: '/student',
    target: '[data-tour="student-profile"]',
    title: 'Tu perfil',
    body: 'Desde el perfil cambias tema, preferencias e instalas Luz Verde en tu celular. También puedes repetir esta guía cuando quieras.'
  }
];
