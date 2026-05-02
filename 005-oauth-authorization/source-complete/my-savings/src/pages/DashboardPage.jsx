import React, { useEffect, useState } from 'react';
import { useAuth } from '../auth/AuthContext.jsx';
import { logout, grantBalanceAccess } from '../auth/userManager.js';

export default function DashboardPage() {
  const { user } = useAuth();
  const [balance, setBalance] = useState(null);
  const [error, setError]     = useState(null);

  // oidc-client-ts exposes the raw scope string granted by the server on user.scope.
  // We check for read:accounts here — if it's present the access token can reach /api/balances.
  const scopes   = (user?.scope ?? '').split(' ');
  const hasScope = scopes.includes('read:accounts');

  const roles = user?.profile?.realm_access?.roles ?? [];

  useEffect(() => {
    if (!hasScope) return;
    fetch(`${process.env.API_BASE_URL}/api/balances`, {
      headers: { Authorization: `Bearer ${user.access_token}` },
    })
      .then(r => r.json())
      .then(data => setBalance(data.balances))
      .catch(e => setError(e.message));
  }, [hasScope, user?.access_token]);

  return (
    <div className="container">
      {/* Profile */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1>{user?.profile?.name ?? user?.profile?.preferred_username}</h1>
          <p style={{ color: '#888', fontSize: '0.9rem', marginTop: 4 }}>{user?.profile?.email}</p>
          <div style={{ marginTop: 8 }}>
            {roles.map(r => <span key={r} className="badge">{r}</span>)}
          </div>
        </div>
        <button className="secondary" onClick={logout}>Sign out</button>
      </div>

      {/* Granted scopes */}
      <div className="card">
        <h2>Granted scopes</h2>
        <div>
          {scopes.filter(s => s).map(s => (
            <span key={s} className="scope-tag">{s}</span>
          ))}
        </div>
      </div>

      {/* Balance */}
      <div className="card">
        <h2>Account Balance</h2>
        {hasScope ? (
          error ? (
            <p className="error">{error}</p>
          ) : balance ? (
            <>
              <div className="balance-row">
                <span>Checking account</span>
                <span className="balance-amount">${balance.checking.toLocaleString()}</span>
              </div>
              <div className="balance-row">
                <span>Savings account</span>
                <span className="balance-amount">${balance.savings.toLocaleString()}</span>
              </div>
            </>
          ) : (
            <p style={{ color: '#888' }}>Loading…</p>
          )
        ) : (
          <div className="notice">
            <p>
              <strong>My Savings</strong> does not yet have permission to read your balance.
              Click below to grant access — Keycloak will show you exactly what you are approving.
            </p>
            <button onClick={grantBalanceAccess}>
              Grant access to balance
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
