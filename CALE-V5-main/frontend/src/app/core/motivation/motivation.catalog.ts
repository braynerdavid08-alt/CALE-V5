import { MotivationTip } from './motivation.model';

/**
 * Tips de conducción segura orientados a formación vial.
 * Priorizan acción concreta sobre eslóganes vacíos.
 */
export const MOTIVATION_CATALOG: readonly MotivationTip[] = [
  {
    id: 'care-rest',
    headline: 'Si bostezas al volante, detente: el descanso también es seguridad.',
    detail: 'El cansancio reduce reflejos como el alcohol. Mejor llegar tarde que no llegar.',
    category: 'cuidado',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'care-stress',
    headline: 'Antes de arrancar, respira: un viaje calmado empieza fuera del carro.',
    detail: 'La prisa y el enojo se convierten en decisiones riesgosas. Regula tu estado primero.',
    category: 'cuidado',
    audience: 'all',
    moment: 'morning'
  },
  {
    id: 'care-night',
    headline: 'De noche tu vista engaña: reduce velocidad y aumenta distancia.',
    detail: 'La fatiga nocturna es silenciosa. Si sientes pesadez, para y descansa.',
    category: 'cuidado',
    audience: 'all',
    moment: 'night'
  },
  {
    id: 'care-family',
    headline: 'Cuidarte en la vía es cuidar a quienes te esperan en casa.',
    detail: 'Cada decisión al volante afecta a más personas de las que ves.',
    category: 'cuidado',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'attn-phone',
    headline: 'El celular puede esperar: unos segundos de distracción cambian una vida.',
    detail: 'Si debes usarlo, detente en un lugar seguro. Ningún mensaje vale una colisión.',
    category: 'atencion',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'attn-rush',
    headline: 'En hora pico la paciencia protege más que el acelerador.',
    detail: 'Mantén distancia, anticipa frenadas y deja pasar el impulso de “ganar segundos”.',
    category: 'atencion',
    audience: 'all',
    moment: 'afternoon'
  },
  {
    id: 'attn-focus',
    headline: 'Una sola tarea al conducir: conducir.',
    detail: 'Comer, maquillarse o revisar mapas en movimiento divide tu atención y multiplica el riesgo.',
    category: 'atencion',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'ant-scan',
    headline: 'Mira lejos, cerca y a los lados: anticipar evita frenazos de último segundo.',
    detail: 'Escanea el entorno cada pocos segundos. La mejor maniobra es la que no necesitas improvisar.',
    category: 'anticipacion',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'ant-distance',
    headline: 'Deja espacio: la distancia es tu colchón de seguridad.',
    detail: 'Si el de adelante frena de golpe, ese margen es lo que te da tiempo de reaccionar.',
    category: 'anticipacion',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'ant-blind',
    headline: 'Los puntos ciegos existen: confirma con mirada antes de cambiar de carril.',
    detail: 'Espejos + revisión rápida por el hombro. Asumir que “no hay nadie” es un error común.',
    category: 'anticipacion',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'res-belt',
    headline: 'Cinturón abrochado antes de moverte: es el hábito que más vidas salva.',
    detail: 'Abróchalo también en trayectos cortos. La mayoría de siniestros ocurren cerca de casa.',
    category: 'respeto',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'res-pedestrian',
    headline: 'El peatón siempre tiene prioridad emocional: cede y salva un susto.',
    detail: 'En intersecciones y zonas escolares, baja la velocidad y espera la confirmación visual.',
    category: 'respeto',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'res-rules',
    headline: 'Respetar las normas no es miedo: es madurez al volante.',
    detail: 'Límites, semáforos y señales existen para coordinar vidas, no para “estorbar”.',
    category: 'respeto',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'res-rain',
    headline: 'Con lluvia, reduce velocidad y evita charcos: el pavimento miente.',
    detail: 'Aumenta la distancia y usa luces. El aquaplaning aparece sin aviso.',
    category: 'respeto',
    audience: 'all',
    moment: 'any'
  },
  {
    id: 'learn-practice',
    headline: 'Practicar en CALE refuerza lo que un día te protegerá en la calle.',
    detail: 'Cada pregunta y cada simulacro entrenan decisiones que no puedes improvisar en tráfico real.',
    category: 'formacion',
    audience: 'Student',
    moment: 'any'
  },
  {
    id: 'learn-errors',
    headline: 'Equivocarte aquí es barato: analiza el error y conviértelo en reflejo.',
    detail: 'Revisa por qué fallaste. Memorizar no basta; entiende el porqué de la norma.',
    category: 'formacion',
    audience: 'Student',
    moment: 'any'
  },
  {
    id: 'learn-consistency',
    headline: 'Mejor 20 minutos diarios de práctica que una noche de memorización.',
    detail: 'La conducción segura se construye con constancia, no con atracones de estudio.',
    category: 'formacion',
    audience: 'Student',
    moment: 'morning'
  },
  {
    id: 'teach-model',
    headline: 'Tu ejemplo forma conductores: enseña calma, no solo contenido.',
    detail: 'Un docente que prioriza prevención transmite una cultura vial que el examen solo no logra.',
    category: 'formacion',
    audience: 'Teacher',
    moment: 'any'
  },
  {
    id: 'teach-feedback',
    headline: 'Corrige con criterio: explica el riesgo real detrás de cada falla.',
    detail: 'Cuando un estudiante yerra, conecta la respuesta con una situación de vía real.',
    category: 'formacion',
    audience: 'Teacher',
    moment: 'any'
  },
  {
    id: 'admin-culture',
    headline: 'Una plataforma bien cuidada también salva decisiones en la vía.',
    detail: 'Contenido claro, bancos actualizados y seguimiento real sostienen una formación seria.',
    category: 'formacion',
    audience: 'Admin',
    moment: 'any'
  },
  {
    id: 'admin-quality',
    headline: 'La calidad de las preguntas define la calidad del conductor que formas.',
    detail: 'Prioriza claridad, imágenes útiles y distracciones realistas en el banco de ítems.',
    category: 'formacion',
    audience: 'Admin',
    moment: 'any'
  }
];
