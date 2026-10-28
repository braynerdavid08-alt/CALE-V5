/** Official mascot + AI assistant name. */
export const MASCOT_NAME = 'Kazu';

export type TourPose = 'explain' | 'celebrate';
export type TourPhase = 'idle' | 'welcome' | 'step' | 'celebrate';
export type TourPersistStatus = 'done' | 'skipped';

export interface TourStep {
  id: string;
  route: string;
  /** CSS selector, typically [data-tour="…"] */
  target: string;
  title: string;
  body: string;
}

export interface TourPersistState {
  status: TourPersistStatus;
  updatedAt: string;
}

export interface TourRuntimeState {
  phase: TourPhase;
  stepIndex: number;
}
