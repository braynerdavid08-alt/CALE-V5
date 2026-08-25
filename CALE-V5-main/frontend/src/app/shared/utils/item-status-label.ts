/** Traduce estados de ítem del API al español de la UI. */
export function itemStatusLabel(status?: string | null): string {
  switch (status) {
    case 'Pending':
      return 'Pendiente';
    case 'Available':
      return 'Disponible';
    case 'InProgress':
      return 'En progreso';
    case 'Submitted':
      return 'Entregado';
    case 'Graded':
      return 'Calificado';
    case 'Expired':
      return 'Vencido';
    case 'Exhausted':
      return 'Agotado';
    default:
      return status?.trim() || '—';
  }
}
