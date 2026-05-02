import React from 'react';
import { useAuth } from '../auth/AuthContext.jsx';
import { logout } from '../auth/userManager.js';
import TransactionsPanel from '../components/TransactionsPanel.jsx';

export default function DashboardPage() {
  const { user } = useAuth();
  const roles = user?.profile?.realm_access?.roles ?? [];

  return (
    <div className="container">
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

      <TransactionsPanel accessToken={user.access_token} />
    </div>
  );
}
