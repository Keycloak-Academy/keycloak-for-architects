import React, { useEffect, useState } from 'react';

export default function PasskeyDetector() {
  const [supportsPasskeys, setSupportsPasskeys] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (typeof window === 'undefined' || !window.PublicKeyCredential) {
      setSupportsPasskeys(false);
      return;
    }

    window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()
      .then((available) => {
        setSupportsPasskeys(available);
      })
      .catch((err) => {
        setError(err.message);
        setSupportsPasskeys(false);
      });
  }, []);

  if (supportsPasskeys === null) {
    return <p style={{ color: '#888' }}>Checking passkey support...</p>;
  }

  if (error) {
    return <p style={{ color: '#c00' }}>Could not detect passkey support: {error}</p>;
  }

  if (supportsPasskeys) {
    return (
      <div
        style={{
          background: '#e6f4ea',
          border: '1px solid #34a853',
          borderRadius: 8,
          padding: '12px 16px',
          marginTop: 16,
          textAlign: 'left',
        }}
      >
        <strong>Passkey-ready device detected</strong>
        <p style={{ margin: '4px 0 0', fontSize: 14, color: '#1e8e3e' }}>
          You can register a passkey for passwordless sign-in after logging in.
        </p>
      </div>
    );
  }

  return (
    <div
      style={{
        background: '#fce8e6',
        border: '1px solid #ea4335',
        borderRadius: 8,
        padding: '12px 16px',
        marginTop: 16,
        textAlign: 'left',
      }}
    >
      <strong>No platform authenticator found</strong>
      <p style={{ margin: '4px 0 0', fontSize: 14, color: '#c5221f' }}>
        This device does not support passkeys. Use a device with Touch ID, Windows Hello, or a security key to complete the WebAuthn tasks.
      </p>
    </div>
  );
}
