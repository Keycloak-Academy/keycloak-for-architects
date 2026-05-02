const API_BASE = process.env.API_BASE_URL;

async function apiFetch(path, accessToken) {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(body.detail || body.error || `HTTP ${res.status}`);
  return body;
}

export const getTransactions = token => apiFetch('/api/transactions', token);
export const getBalances     = token => apiFetch('/api/balances', token);
