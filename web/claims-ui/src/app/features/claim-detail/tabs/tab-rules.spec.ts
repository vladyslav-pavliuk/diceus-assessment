import { ClaimParty } from '../../../core/models/claim.models';
import { relatedTab } from './audit-tab';
import { MAX_DOCUMENT_BYTES, precheckDocument } from './documents-tab';
import { canRemoveParty } from './parties-tab';

function party(id: string, partyRole: ClaimParty['partyRole'], isActive = true): ClaimParty {
  return {
    id,
    partyRole,
    isActive,
    partyType: 'Person',
    firstName: id,
    lastName: 'X',
    companyName: null,
    displayName: id,
    email: null,
    phone: null,
    notes: null,
  };
}

describe('UI-DET-04 / PTY-01 remove party', () => {
  it('UI_DET_04_Remove_disabled_for_last_claimant', () => {
    const parties = [party('claimant', 'Claimant'), party('witness', 'Witness')];
    expect(canRemoveParty(parties[0], parties)).toBe(false);
    expect(canRemoveParty(parties[1], parties)).toBe(true);
  });

  it('UI_DET_04_A_claimant_can_go_while_another_active_claimant_remains', () => {
    const parties = [party('a', 'Claimant'), party('b', 'Claimant'), party('c', 'Claimant', false)];
    expect(canRemoveParty(parties[0], parties)).toBe(true);
    expect(canRemoveParty(parties[2], parties)).toBe(false); // already inactive
  });
});

describe('UI-DET-11 document pre-check (FRS §13)', () => {
  it('DOC_Allowlisted_extensions_up_to_50_MB_pass', () => {
    expect(precheckDocument({ name: 'report.PDF', size: 10 })).toBeNull();
    expect(precheckDocument({ name: 'photo.jpeg', size: MAX_DOCUMENT_BYTES })).toBeNull();
  });

  it('DOC_Other_types_empty_and_oversized_files_are_refused_before_upload', () => {
    expect(precheckDocument({ name: 'macro.docm', size: 10 })).toContain('Only');
    expect(precheckDocument({ name: 'noextension', size: 10 })).toContain('Only');
    expect(precheckDocument({ name: 'empty.txt', size: 0 })).toBe('The file is empty.');
    expect(precheckDocument({ name: 'big.pdf', size: MAX_DOCUMENT_BYTES + 1 })).toBe(
      'The file is larger than 50 MB.',
    );
  });
});

describe('UI-DET-12 audit related-entity links', () => {
  it('UI_DET_12_Related_entities_link_to_their_tab', () => {
    expect(relatedTab('ReserveTransaction')).toBe('reserves');
    expect(relatedTab('ClaimDocument')).toBe('documents');
    expect(relatedTab('ClaimParty')).toBe('parties');
    expect(relatedTab('ClaimValidationIssue')).toBe('overview');
    expect(relatedTab(null)).toBeNull();
  });
});
