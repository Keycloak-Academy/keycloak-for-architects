import React from 'react';
import { useAuth } from '../auth/AuthContext.jsx';
import { login } from '../auth/userManager.js';
import { Navigate } from 'react-router-dom';

export default function LoginPage() {
  const { user, isLoading } = useAuth();
  if (isLoading) return null;
  if (user) return <Navigate to="/dashboard" replace />;

  return (
    <div className="container" style={{ display: 'flex', justifyContent: 'center', marginTop: 80 }}>
      <div className="card" style={{ width: 380, textAlign: 'center' }}>
        <h1>Banking App</h1>
        <p style={{ color: '#888', margin: '12px 0 24px' }}>
          Public client · Authorization Code + PKCE
        </p>
        <button onClick={login} style={{ width: '100%', padding: '12px' }}>
          Sign in with Keycloak
        </button>
      </div>
    </div>
  );
}
