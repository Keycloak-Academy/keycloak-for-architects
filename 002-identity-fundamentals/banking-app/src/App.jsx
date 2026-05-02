import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useAuth, AuthProvider } from './auth/AuthContext.jsx';
import LoginPage from './pages/LoginPage.jsx';
import CallbackPage from './pages/CallbackPage.jsx';
import DashboardPage from './pages/DashboardPage.jsx';

function ProtectedRoute({ children }) {
  const { user, isLoading } = useAuth();
  if (isLoading) return <div className="container"><p>Loading...</p></div>;
  return user ? children : <Navigate to="/" replace />;
}

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/"          element={<LoginPage />} />
          <Route path="/callback"  element={<CallbackPage />} />
          <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
