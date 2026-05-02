import React, { useEffect, useState } from 'react';
import { getBalances } from '../api/bankingApi.js';

export default function BalancesPanel({ accessToken }) {
  const [balances, setBalances] = useState(null);
  const [error, setError]       = useState(null);

  useEffect(() => {
    getBalances(accessToken)
      .then(data => setBalances(data.balances))
      .catch(e => setError(e.message));
  }, [accessToken]);

  return (
    <div className="card">
      <h2>Balances</h2>
      <p style={{ color: '#888', fontSize: '0.8rem', marginBottom: 12 }}>
        Protected by scope: <span className="tag">read:accounts</span>
      </p>
      {error ? (
        <p className="error">
          {error}
          {error.includes('403') || error.includes('Forbidden')
            ? ' — Request the "read:accounts" scope and grant consent in Keycloak.'
            : ''}
        </p>
      ) : !balances ? (
        <p style={{ color: '#aaa', fontSize: '0.9rem' }}>Loading…</p>
      ) : (
        <table>
          <thead>
            <tr><th>Account</th><th>Balance</th></tr>
          </thead>
          <tbody>
            <tr>
              <td>Checking</td>
              <td>${balances.checking.toLocaleString()}</td>
            </tr>
            <tr>
              <td>Savings</td>
              <td>${balances.savings.toLocaleString()}</td>
            </tr>
          </tbody>
        </table>
      )}
    </div>
  );
}
