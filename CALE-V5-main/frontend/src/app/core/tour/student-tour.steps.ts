import { MASCOT_NAME, TourStep } from './tour.types';

/**
 * Student tour v5: one step per home feature (top → bottom), narrated by Kazu.
 * Adds “Vincula tu escuela” for new accounts and highlights “Mi formación”.
 */
export const STUDENT_TOUR_STEPS: TourStep[] = [
  {
    id: 'daily',
    route: '/student',
    target: '[data-tour="student-daily"]',
    title: 'Jugar reto diario',
    body: 'Practica 5 preguntas al día. Así mantienes tu racha y entras en calor antes del examen.'
  },
  {
    id: 'simulator',
    route: '/student',
    target: '[data-tour="student-simulator"]',
    title: 'Hacer simulacro',
    body: 'Aquí están los exámenes de práctica, incluido el de tu escuela. Entrénalo como el día real.'
  },
  {
    id: 'shop',
    route: '/student',
    target: '[data-tour="student-shop"]',
    title: 'Tienda Luz Verde',
    body: 'Cascos, guantes y equipo para moteros con descuento. Con cada compra ayudas a financiar la app.'
  },
  {
    id: 'link-school',
    route: '/student',
    target: '[data-tour="student-link-school"]',
    title: 'Vincula tu escuela',
    body: 'Si estudias en un CEA, vincúlalo aquí con el NIT o el correo de la escuela. Así ves clases, agenda y tu proceso de formación.'
  },
  {
    id: 'mistakes',
    route: '/student',
    target: '[data-tour="student-mistakes"]',
    title: 'Repaso de errores',
    body: 'Vuelve a las preguntas que fallaste hasta dominarlas. Es el atajo más claro para subir tu nivel.'
  },
  {
    id: 'ranking',
    route: '/student',
    target: '[data-tour="student-ranking"]',
    title: 'Ranking',
    body: 'Mira tu puesto frente a otros aprendices. Ganas XP practicando: reto, simulacro y juegos suman.'
  },
  {
    id: 'games',
    route: '/student',
    target: '[data-tour="student-games"]',
    title: 'Juegos',
    body: 'Aprende jugando: movilidad, señales, duelos y logros. Practica normas sin sentirlo como un examen.'
  },
  {
    id: 'courses',
    route: '/student',
    target: '[data-tour="student-courses"]',
    title: 'Cursos virtuales',
    body: 'Temas de la escuela por lecciones, con videos y actividades. Estudia a tu ritmo, cuando quieras.'
  },
  {
    id: 'live',
    route: '/student',
    target: '[data-tour="student-live"]',
    title: 'Aula en vivo',
    body: 'Cuando tu instructor abra la clase, entra con el código o escaneando el QR.'
  },
  {
    id: 'classes',
    route: '/student',
    target: '[data-tour="student-classes"]',
    title: 'Mis clases',
    body: 'Materiales y grupos de tu profesor o CEA. Aquí revisas lo que te dejan para la clase.'
  },
  {
    id: 'library',
    route: '/student',
    target: '[data-tour="student-library"]',
    title: 'Biblioteca jurídica',
    body: 'Normas de tránsito y la Ley 769 para consultar cuando estudies o tengas una duda concreta.'
  },
  {
    id: 'formacion',
    route: '/student',
    target: '[data-tour="student-formacion"]',
    title: 'Mi formación',
    body: 'Aquí ves las etapas de tu curso, requisitos, avance y la agenda de teoría y manejo con tu escuela.'
  },
  {
    id: 'exam',
    route: '/student',
    target: '[data-tour="student-exam"]',
    title: 'Mi examen teórico',
    body: 'Aquí agendas o consultas tu cita del examen teórico con la escuela.'
  },
  {
    id: 'kazu',
    route: '/student',
    target: '[data-tour="student-kazu"]',
    title: `Habla con ${MASCOT_NAME}`,
    body: `${MASCOT_NAME} te ayuda con horarios y dudas de formación. Siempre te pedirá confirmar antes de hacer un cambio.`
  }
];
