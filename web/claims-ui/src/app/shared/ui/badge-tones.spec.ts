import { STATUS_COLOURS } from './badge-tones';

describe('UI-LIST-02 status badge colours', () => {
  it('UI_LIST_02_Status_badge_colours are exactly those of FRS §11.1', () => {
    expect(STATUS_COLOURS).toEqual({
      Draft: 'grey',
      Open: 'blue',
      UnderInvestigation: 'orange',
      PendingPayment: 'purple',
      Closed: 'green',
      Reopened: 'amber',
      Withdrawn: 'dark-grey',
    });
  });
});
