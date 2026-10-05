import React, { useState } from 'react';
import { ApiService } from '../../services/api/ApiService';

interface AuthScreenProps {
  onAuthenticated: () => void;
}

export const AuthScreen: React.FC<AuthScreenProps> = ({ onAuthenticated }) => {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    const trimmedUser = username.trim();
    if (trimmedUser.length < 3 || trimmedUser.length > 32) {
      setErrorMessage('Tên đăng nhập phải từ 3 đến 32 ký tự.');
      return;
    }

    if (password.length < 6) {
      setErrorMessage('Mật khẩu phải có ít nhất 6 ký tự.');
      return;
    }

    setLoading(true);

    try {
      const api = ApiService.getInstance();
      if (mode === 'login') {
        await api.login({ username: trimmedUser, password });
      } else {
        await api.register({ username: trimmedUser, password });
      }

      onAuthenticated();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes('USERNAME_ALREADY_TAKEN')) {
        setErrorMessage('Tên đăng nhập đã tồn tại. Vui lòng chọn tên khác.');
      } else if (msg.includes('INVALID_CREDENTIALS')) {
        setErrorMessage('Tên đăng nhập hoặc mật khẩu không chính xác.');
      } else if (msg.includes('INVALID_INPUT')) {
        setErrorMessage('Dữ liệu không hợp lệ. Vui lòng kiểm tra lại thông tin.');
      } else {
        setErrorMessage(`Đăng nhập thất bại: ${msg}`);
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-screen-container" data-testid="auth-screen">
      <div className="auth-card">
        <div className="auth-header">
          <h1 className="auth-title">Đại Chiến Anime Cổ Tích</h1>
          <p className="auth-subtitle">
            {mode === 'login' ? 'Đăng nhập tài khoản để vào chiến trận' : 'Tạo tài khoản phiêu lưu mới'}
          </p>
        </div>

        <div className="auth-tabs">
          <button
            type="button"
            className={`auth-tab ${mode === 'login' ? 'active' : ''}`}
            onClick={() => {
              setMode('login');
              setErrorMessage(null);
            }}
            data-testid="tab-login"
          >
            Đăng nhập
          </button>
          <button
            type="button"
            className={`auth-tab ${mode === 'register' ? 'active' : ''}`}
            onClick={() => {
              setMode('register');
              setErrorMessage(null);
            }}
            data-testid="tab-register"
          >
            Đăng ký
          </button>
        </div>

        {errorMessage ? (
          <div className="auth-error-banner" data-testid="auth-error-banner" role="alert">
            {errorMessage}
          </div>
        ) : null}

        <form onSubmit={handleSubmit} className="auth-form" data-testid="auth-form">
          <div className="auth-field">
            <label htmlFor="auth-username">Tên đăng nhập</label>
            <input
              id="auth-username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Nhập tên tài khoản (3-32 ký tự)"
              disabled={loading}
              autoComplete="username"
              required
              data-testid="input-username"
            />
          </div>

          <div className="auth-field">
            <label htmlFor="auth-password">Mật khẩu</label>
            <input
              id="auth-password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Nhập mật khẩu (tối thiểu 6 ký tự)"
              disabled={loading}
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              required
              data-testid="input-password"
            />
          </div>

          <button
            type="submit"
            className="auth-submit-button"
            disabled={loading}
            data-testid="button-submit"
          >
            {loading
              ? 'Đang xử lý...'
              : mode === 'login'
                ? 'Vào Trận Chiến'
                : 'Khởi Tạo Tài Khoản'}
          </button>
        </form>

        <div className="auth-footer">
          {mode === 'login' ? (
            <p>
              Chưa có tài khoản?{' '}
              <button
                type="button"
                className="auth-link-button"
                onClick={() => {
                  setMode('register');
                  setErrorMessage(null);
                }}
              >
                Đăng ký ngay
              </button>
            </p>
          ) : (
            <p>
              Đã có tài khoản?{' '}
              <button
                type="button"
                className="auth-link-button"
                onClick={() => {
                  setMode('login');
                  setErrorMessage(null);
                }}
              >
                Đăng nhập
              </button>
            </p>
          )}
        </div>
      </div>
    </div>
  );
};
