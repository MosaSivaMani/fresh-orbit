// Response bodies shaped like docs/api examples, shared by tests.

export function emptyDashboard() {
  return {
    asOf: "2026-10-24T05:00:00Z",
    date: "2026-10-24",
    filters: {},
    ordersByStatus: [],
    slots: [],
    lowStock: [],
    openComplaints: 0,
    refundsRequested: 0,
    refundsRequestedPaise: 0,
    openPaymentIssues: 0,
  };
}

export function emptyReport() {
  return {
    asOf: null,
    fromDate: "2026-10-24",
    toDate: "2026-10-24",
    filters: {},
    totals: {},
    items: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
  };
}

export function page<T>(
  items: T[],
  extra: Partial<{ page: number; pageSize: number; totalCount: number }> = {},
) {
  return { items, page: 1, pageSize: 20, totalCount: items.length, ...extra };
}
