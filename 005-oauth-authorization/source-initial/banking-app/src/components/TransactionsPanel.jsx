import React, { useEffect, useState } from 'react';
import { getTransactions } from '../api/bankingApi.js';

export default function TransactionsPanel({ accessToken }) {
  const [transactions, setTransactions] = useState([]);
  const [error, setError]               = useState(null);

  useEffect(() => {
    getTransactions(accessToken)
      .then(data => setTransactions(data.transactions ?? []))
      .catch(e => setError(e.message));
  }, [accessToken]);

  return (
    <div className="card">
      <h2>Transactions</h2>
      <p style={{ color: '#888', fontSize: '0.8rem', marginBottom: 12 }}>
        Protected by role: <span className="tag">customer</span>
      </p>
      {error ? (
        <p className="error">
          {error}
          {error.includes('403') || error.includes('Forbidden')
            ? ' — Log in as a user with the customer realm role.'
            : ''}
        </p>
      ) : transactions.length === 0 ? (
        <p style={{ color: '#aaa', fontSize: '0.9rem' }}>Loading…</p>
      ) : (
        <table>
          <thead>
            <tr><th>ID</th><th>Description</th><th>Amount</th><th>Date</th></tr>
          </thead>
          <tbody>
            {transactions.map(t => (
              <tr key={t.id}>
                <td>{t.id}</td>
                <td>{t.description}</td>
                <td style={{ color: t.amount < 0 ? '#c62828' : '#2e7d32' }}>
                  {t.amount < 0 ? `-$${Math.abs(t.amount)}` : `+$${t.amount}`}
                </td>
                <td style={{ color: '#888' }}>{t.date}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
