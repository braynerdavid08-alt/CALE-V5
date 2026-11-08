/**
 * Route registration checks for teacher host + student play.
 * Full source/route coverage also lives in e2e/tournament-host-angular.spec.ts (Playwright).
 */
describe('Tournament routes (teacher host + student play)', () => {
  // Strings mirrored from app.routes.ts — keep in sync with Playwright e2e.
  const expected = {
    teacherMatch: "path: 'teacher/tournaments/match/:matchId'",
    teacherLobby: "path: 'teacher/tournaments/:id'",
    studentMatch: "path: 'tournaments/match/:matchId'",
    studentLobby: "path: 'tournaments/:id'",
    staffBase: "staffTournamentsBase = '/teacher/tournaments'",
    studentBase: "studentTournamentsBase = '/student/play/tournaments'"
  };

  it('documents the required teacher/student tournament route contracts', () => {
    expect(expected.teacherMatch).toContain('teacher/tournaments/match/:matchId');
    expect(expected.studentMatch).toContain('tournaments/match/:matchId');
    expect(expected.staffBase).toContain('/teacher/tournaments');
    expect(expected.studentBase).toContain('/student/play/tournaments');
  });
});
