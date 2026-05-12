import React from 'react';
import { useAuth } from '../auth/AuthContext.jsx';
import { logout } from '../auth/userManager.js';

export default function DashboardPage() {
  const { user } = useAuth();
  const profile  = user?.profile ?? {};

  const orgClaim  = profile.organization ?? {};
  const orgSlug   = Object.keys(orgClaim)[0];

  return (
    <div className="container">
      <div className="card">
        <h1>Hello, {profile.given_name ?? profile.preferred_username}!</h1>

        {orgSlug
          ? <div className="tenant">{orgSlug}</div>
          : <p style={{ color: '#bbb', margin: '16px 0' }}>No tenant — assign the <code>organization</code> scope to this client</p>
        }

        <button className="secondary" onClick={logout}>Sign out</button>
      </div>
    </div>
  );
}
