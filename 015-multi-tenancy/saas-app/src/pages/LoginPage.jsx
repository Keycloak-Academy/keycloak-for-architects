import React from 'react';
import { useAuth } from '../auth/AuthContext.jsx';
import { login } from '../auth/userManager.js';
import { Navigate } from 'react-router-dom';

export default function LoginPage() {
  const { user, isLoading } = useAuth();
  if (isLoading) return null;
  if (user) return <Navigate to="/dashboard" replace />;

  return (
    <div className="container">
      <div className="card">
        <h1>SaaS App</h1>
        <p>Sign in to see your tenant</p>
        <button onClick={login} style={{ width: '100%', padding: '12px' }}>
          Sign in with Keycloak
        </button>
      </div>
    </div>
  );
}
