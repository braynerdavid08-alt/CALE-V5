import {
  EditorSlide,
  SlideBackground,
  SlideElement,
  newClientId,
  parseBackground,
  parseElements,
  scaleElementsFromLegacy
} from './presentation.models';

function slide(
  title: string,
  background: SlideBackground,
  elementsJson: string,
  notes = ''
): EditorSlide {
  return {
    clientId: newClientId('slide'),
    title,
    notes,
    background,
    elements: scaleElementsFromLegacy(parseElements(elementsJson))
  };
}

/** Mirrors backend PresentationTemplates for "add slide" in the editor. */
export function buildSlideFromTemplate(templateKey: string, slideNumber: number): EditorSlide {
  const key = (templateKey || 'blank').trim().toLowerCase();
  const n = slideNumber;

  switch (key) {
    case 'cover':
    case 'portada':
      return slide(
        'Portada',
        parseBackground('{"type":"solid","color":"#0B1F33"}'),
        `[{"id":"el-brand","type":"text","x":80,"y":120,"w":800,"h":40,"rotation":0,"z":1,"props":{"text":"Luz Verde · Formación vial","fontSize":18,"fontWeight":600,"color":"#7EC8E3","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-title","type":"text","x":80,"y":200,"w":800,"h":100,"rotation":0,"z":2,"props":{"text":"Título de la clase","fontSize":48,"fontWeight":700,"color":"#FFFFFF","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-sub","type":"text","x":80,"y":320,"w":700,"h":60,"rotation":0,"z":3,"props":{"text":"Normas · Señales · Conducción segura","fontSize":22,"fontWeight":400,"color":"#C9D6E3","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-bar","type":"shape","x":80,"y":400,"w":160,"h":8,"rotation":0,"z":4,"props":{"shape":"rect","fill":"#2BB0ED","stroke":"transparent","strokeWidth":0,"opacity":1}}]`
      );
    case 'title-content':
    case 'titulo-contenido':
      return slide(
        'Título + contenido',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":64,"y":48,"w":832,"h":64,"rotation":0,"z":1,"props":{"text":"Concepto clave","fontSize":36,"fontWeight":700,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-body","type":"text","x":64,"y":140,"w":832,"h":320,"rotation":0,"z":2,"props":{"text":"• Punto uno\\n• Punto dos\\n• Punto tres","fontSize":24,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}}]`
      );
    case 'signal':
    case 'senal':
      return slide(
        'Señal de tránsito',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":64,"y":36,"w":832,"h":56,"rotation":0,"z":1,"props":{"text":"Señal de tránsito","fontSize":34,"fontWeight":700,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-box","type":"shape","x":96,"y":120,"w":220,"h":220,"rotation":0,"z":2,"props":{"shape":"rect","fill":"#E8F4FC","stroke":"#2BB0ED","strokeWidth":2,"opacity":1}},{"id":"el-hint","type":"text","x":110,"y":200,"w":190,"h":80,"rotation":0,"z":3,"props":{"text":"Inserta la foto\\ndesde Señales","fontSize":16,"fontWeight":600,"color":"#2BB0ED","align":"center","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-meaning","type":"text","x":360,"y":140,"w":520,"h":100,"rotation":0,"z":4,"props":{"text":"Significado\\nDescribe qué indica la señal.","fontSize":20,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-example","type":"text","x":360,"y":280,"w":520,"h":140,"rotation":0,"z":5,"props":{"text":"Ejemplo práctico\\nCuándo y cómo debe actuar el conductor.","fontSize":20,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}}]`
      );
    case 'intersection':
    case 'interseccion':
      return slide(
        'Intersección / prioridad',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":64,"y":28,"w":832,"h":48,"rotation":0,"z":1,"props":{"text":"Prioridad en la intersección","fontSize":32,"fontWeight":700,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-road-h","type":"shape","x":80,"y":220,"w":520,"h":70,"rotation":0,"z":2,"props":{"shape":"rect","fill":"#607D8B","stroke":"transparent","strokeWidth":0,"opacity":1}},{"id":"el-road-v","type":"shape","x":280,"y":100,"w":70,"h":300,"rotation":0,"z":3,"props":{"shape":"rect","fill":"#607D8B","stroke":"transparent","strokeWidth":0,"opacity":1}},{"id":"el-mark","type":"shape","x":300,"y":245,"w":30,"h":12,"rotation":0,"z":4,"props":{"shape":"rect","fill":"#FFFFFF","stroke":"transparent","strokeWidth":0,"opacity":0.9}},{"id":"el-car","type":"shape","x":120,"y":232,"w":90,"h":42,"rotation":0,"z":5,"props":{"shape":"rect","fill":"#2BB0ED","stroke":"#0B1F33","strokeWidth":2,"opacity":1}},{"id":"el-moto","type":"shape","x":300,"y":130,"w":36,"h":70,"rotation":0,"z":6,"props":{"shape":"rect","fill":"#FF7043","stroke":"#0B1F33","strokeWidth":2,"opacity":1}},{"id":"el-panel","type":"shape","x":640,"y":100,"w":280,"h":300,"rotation":0,"z":7,"props":{"shape":"rect","fill":"#E8F4FC","stroke":"#2BB0ED","strokeWidth":2,"opacity":1}},{"id":"el-guide","type":"text","x":660,"y":120,"w":240,"h":260,"rotation":0,"z":8,"props":{"text":"Pregunta al grupo\\n\\n1. ¿Quién tiene prioridad?\\n2. ¿Hay señal o semáforo?\\n3. ¿Qué harías tú?","fontSize":18,"fontWeight":400,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}}]`
      );
    case 'case':
    case 'caso':
      return slide(
        'Caso práctico',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":64,"y":40,"w":832,"h":56,"rotation":0,"z":1,"props":{"text":"Analiza la siguiente situación","fontSize":32,"fontWeight":700,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-box","type":"shape","x":64,"y":120,"w":832,"h":180,"rotation":0,"z":2,"props":{"shape":"rect","fill":"#E8F4FC","stroke":"#2BB0ED","strokeWidth":2,"opacity":1}},{"id":"el-desc","type":"text","x":88,"y":140,"w":784,"h":140,"rotation":0,"z":3,"props":{"text":"Describe el escenario vial aquí…","fontSize":22,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-q","type":"text","x":64,"y":330,"w":832,"h":120,"rotation":0,"z":4,"props":{"text":"Preguntas para el grupo:\\n1. ¿Quién tiene prioridad?\\n2. ¿Qué riesgo identificas?","fontSize":20,"fontWeight":400,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}}]`
      );
    case 'risk':
    case 'escena-riesgo':
      return slide(
        'Escena de riesgo',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":64,"y":28,"w":832,"h":48,"rotation":0,"z":1,"props":{"text":"Identifica el riesgo","fontSize":32,"fontWeight":700,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-road","type":"shape","x":64,"y":200,"w":560,"h":90,"rotation":0,"z":2,"props":{"shape":"rect","fill":"#607D8B","stroke":"transparent","strokeWidth":0,"opacity":1}},{"id":"el-car","type":"shape","x":120,"y":214,"w":120,"h":50,"rotation":0,"z":3,"props":{"shape":"rect","fill":"#2BB0ED","stroke":"#0B1F33","strokeWidth":2,"opacity":1}},{"id":"el-cone","type":"shape","x":300,"y":160,"w":60,"h":80,"rotation":0,"z":4,"props":{"shape":"triangle","fill":"#FF6D00","stroke":"#FFFFFF","strokeWidth":3,"opacity":1}},{"id":"el-person","type":"text","x":420,"y":140,"w":100,"h":100,"rotation":0,"z":5,"props":{"text":"🚶","fontSize":56,"fontWeight":400,"color":"#0B1F33","align":"center","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-callout","type":"shape","x":660,"y":100,"w":260,"h":280,"rotation":0,"z":6,"props":{"shape":"rect","fill":"#FFF3E0","stroke":"#FB8C00","strokeWidth":2,"opacity":1}},{"id":"el-risk-text","type":"text","x":680,"y":120,"w":220,"h":240,"rotation":0,"z":7,"props":{"text":"Riesgos posibles\\n\\n• Peatón en vía\\n• Obra sin señalizar\\n• Exceso de velocidad\\n\\n¿Cuál actúa primero?","fontSize":18,"fontWeight":400,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}}]`
      );
    case 'quiz':
    case 'pregunta':
      return slide(
        'Evaluación rápida',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":64,"y":48,"w":832,"h":80,"rotation":0,"z":1,"props":{"text":"¿Quién tiene la prioridad?","fontSize":32,"fontWeight":700,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-a","type":"text","x":64,"y":160,"w":832,"h":48,"rotation":0,"z":2,"props":{"text":"A) El vehículo que llega por la derecha","fontSize":22,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-b","type":"text","x":64,"y":220,"w":832,"h":48,"rotation":0,"z":3,"props":{"text":"B) Quien ya circula por la vía principal","fontSize":22,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-c","type":"text","x":64,"y":280,"w":832,"h":48,"rotation":0,"z":4,"props":{"text":"C) El de mayor tamaño","fontSize":22,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-d","type":"text","x":64,"y":340,"w":832,"h":48,"rotation":0,"z":5,"props":{"text":"D) Quien toque el claxon primero","fontSize":22,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-note","type":"text","x":64,"y":430,"w":832,"h":40,"rotation":0,"z":6,"props":{"text":"Respuesta (solo en notas del instructor)","fontSize":14,"fontWeight":400,"color":"#6B7C8F","align":"left","fontFamily":"Segoe UI, sans-serif"}}]`,
        'Respuesta correcta: B'
      );
    case 'quiz-live':
    case 'pregunta-vivo':
      return slide(
        'Pregunta en vivo',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-q","type":"question","x":80,"y":50,"w":800,"h":440,"rotation":0,"z":1,"props":{"text":"¿Qué indica esta señal o escena?","options":["Opción A","Opción B","Opción C","Opción D"],"correctIndex":0,"kind":"mcq","explanation":"Explica la respuesta correcta aquí.","topic":"Señales","autoOpen":true,"imageUrl":null}}]`,
        'Sube una imagen de apoyo en el panel derecho de la pregunta.'
      );
    case 'compare':
    case 'comparacion':
      return slide(
        'Comparación',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":64,"y":36,"w":832,"h":48,"rotation":0,"z":1,"props":{"text":"Comparación","fontSize":32,"fontWeight":700,"color":"#0B1F33","align":"center","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-l","type":"shape","x":64,"y":110,"w":400,"h":340,"rotation":0,"z":2,"props":{"shape":"rect","fill":"#E8F4FC","stroke":"#2BB0ED","strokeWidth":2,"opacity":1}},{"id":"el-r","type":"shape","x":496,"y":110,"w":400,"h":340,"rotation":0,"z":3,"props":{"shape":"rect","fill":"#FFF3E0","stroke":"#FB8C00","strokeWidth":2,"opacity":1}},{"id":"el-lt","type":"text","x":88,"y":130,"w":352,"h":40,"rotation":0,"z":4,"props":{"text":"Correcto","fontSize":22,"fontWeight":700,"color":"#0B1F33","align":"center","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-rt","type":"text","x":520,"y":130,"w":352,"h":40,"rotation":0,"z":5,"props":{"text":"Incorrecto","fontSize":22,"fontWeight":700,"color":"#0B1F33","align":"center","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-lb","type":"text","x":88,"y":190,"w":352,"h":220,"rotation":0,"z":6,"props":{"text":"Describe la conducta segura…","fontSize":18,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-rb","type":"text","x":520,"y":190,"w":352,"h":220,"rotation":0,"z":7,"props":{"text":"Describe el error frecuente…","fontSize":18,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}}]`
      );
    case 'summary':
    case 'resumen':
      return slide(
        'Resumen',
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":64,"y":48,"w":832,"h":56,"rotation":0,"z":1,"props":{"text":"Resumen de la clase","fontSize":34,"fontWeight":700,"color":"#0B1F33","align":"left","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-body","type":"text","x":64,"y":140,"w":832,"h":300,"rotation":0,"z":2,"props":{"text":"1. …\\n2. …\\n3. …","fontSize":24,"fontWeight":400,"color":"#243447","align":"left","fontFamily":"Segoe UI, sans-serif"}}]`
      );
    case 'closing':
    case 'cierre':
      return slide(
        'Cierre',
        parseBackground('{"type":"solid","color":"#0B1F33"}'),
        `[{"id":"el-title","type":"text","x":80,"y":180,"w":800,"h":80,"rotation":0,"z":1,"props":{"text":"¡Buen viaje y manejo seguro!","fontSize":40,"fontWeight":700,"color":"#FFFFFF","align":"center","fontFamily":"Segoe UI, sans-serif"}},{"id":"el-sub","type":"text","x":80,"y":280,"w":800,"h":60,"rotation":0,"z":2,"props":{"text":"Luz Verde · Formación que impulsa tu camino.","fontSize":20,"fontWeight":400,"color":"#7EC8E3","align":"center","fontFamily":"Segoe UI, sans-serif"}}]`
      );
    default:
      return slide(
        `Diapositiva ${n}`,
        parseBackground('{"type":"solid","color":"#F7F9FC"}'),
        `[{"id":"el-title","type":"text","x":80,"y":200,"w":800,"h":80,"rotation":0,"z":1,"props":{"text":"Haz doble clic para editar","fontSize":36,"fontWeight":600,"color":"#0B1F33","align":"center","fontFamily":"Segoe UI, sans-serif"}}]`
      );
  }
}

export function reassignElementIds(slide: EditorSlide): EditorSlide {
  return {
    ...slide,
    clientId: newClientId('slide'),
    elements: slide.elements.map((el) => ({
      ...el,
      id: newClientId('el'),
      props: { ...el.props } as SlideElement['props']
    }))
  };
}
