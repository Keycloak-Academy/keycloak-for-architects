import React from 'react';
import { useAuth } from '../auth/AuthContext.jsx';
import { logout } from '../auth/userManager.js';

export default function DashboardPage() {
  const { user } = useAuth();
  const profile = user?.profile ?? {};
  const roles = profile.realm_access?.roles ?? [];

  return (
    <div className="container" style={{ maxWidth: 520, marginTop: 60 }}>
      <div className="card" style={{ textAlign: 'center' }}>
        <h1>Hello, {profile.given_name ?? profile.preferred_username}!</h1>
        <p style={{ color: '#888', marginTop: 8 }}>{profile.email}</p>
        {roles.length > 0 && (
          <div style={{ marginTop: 12 }}>
            {roles.map(r => <span key={r} className="badge">{r}</span>)}
          </div>
        )}
        <button className="secondary" onClick={logout} style={{ marginTop: 24 }}>
          Sign out
        </button>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h2>ID token claims</h2>
        <pre>{JSON.stringify(profile, null, 2)}</pre>
      </div>
    </div>
  );
}
