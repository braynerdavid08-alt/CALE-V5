import { resolveMediaUrl } from '../../core/media/resolve-media-url';

/** Profile photo URL for tournament UI, or null to show initials. */
export function tournamentPhotoSrc(photoUrl?: string | null): string | null {
  return resolveMediaUrl(photoUrl) || null;
}

export function tournamentInitials(name?: string | null): string {
  const parts = (name ?? '').trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '?';
  if (parts.length === 1) return parts[0].slice(0, 1).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}
