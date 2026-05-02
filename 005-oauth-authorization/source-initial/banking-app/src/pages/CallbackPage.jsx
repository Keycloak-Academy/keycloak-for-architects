import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { userManager } from '../auth/userManager.js';

export default function CallbackPage() {
  const navigate = useNavigate();

  useEffect(() => {
    // Reads `code` + `state` from the URL, exchanges code for tokens,
    // verifies state (CSRF), stores User in sessionStorage.
    userManager.signinRedirectCallback()
      .then(() => navigate('/dashboard', { replace: true }))
      .catch(err => {
        console.error('Callback error:', err);
        navigate('/', { replace: true });
      });
  }, [navigate]);

  return <div className="container"><p>Completing sign-in...</p></div>;
}
