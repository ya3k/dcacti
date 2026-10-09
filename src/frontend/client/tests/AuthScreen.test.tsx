import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { AuthScreen } from '../src/ui/components/AuthScreen';

/**
 * The messages the server's `API_CONTRACTS.md` §6 error envelope carries into
 * `ApiService.register()`/`login()`, which rethrow `new Error(err?.error)` — so
 * the caught error's `message` is the machine-readable code itself.
 */
const USERNAME_ALREADY_EXISTS = 'USERNAME_ALREADY_EXISTS';
const INVALID_CREDENTIALS = 'INVALID_CREDENTIALS';
const INVALID_INPUT = 'INVALID_INPUT';
const UNRECOGNIZED_CODE = 'SOMETHING_ELSE_FAILED';

const DUPLICATE_USERNAME_MESSAGE = 'Tên đăng nhập đã tồn tại. Vui lòng chọn tên khác.';
const INVALID_CREDENTIALS_MESSAGE = 'Tên đăng nhập hoặc mật khẩu không chính xác.';
const INVALID_INPUT_MESSAGE = 'Dữ liệu không hợp lệ. Vui lòng kiểm tra lại thông tin.';

/** `mockRegister`/`mockLogin` stand in for the singleton's two auth calls. */
const mockRegister = vi.fn();
const mockLogin = vi.fn();

vi.mock('../src/services/api/ApiService', () => ({
  ApiService: {
    getInstance: () => ({ register: mockRegister, login: mockLogin }),
  },
}));

/** Renders `AuthScreen` and returns the `onAuthenticated` spy. */
function renderAuthScreen() {
  const onAuthenticated = vi.fn();
  render(<AuthScreen onAuthenticated={onAuthenticated} />);
  return onAuthenticated;
}

/** Fills both fields with values that pass the local validation gates. */
function fillCredentials(username = 'playerone', password = 'secret123') {
  fireEvent.change(screen.getByTestId('input-username'), { target: { value: username } });
  fireEvent.change(screen.getByTestId('input-password'), { target: { value: password } });
}

function submit() {
  fireEvent.click(screen.getByTestId('button-submit'));
}

function errorBannerText(): string {
  return screen.getByTestId('auth-error-banner').textContent ?? '';
}

describe('AuthScreen (authentication error mapping)', () => {
  beforeEach(() => {
    mockRegister.mockReset();
    mockLogin.mockReset();
  });

  it('maps the authoritative USERNAME_ALREADY_EXISTS code to the duplicate-username message', async () => {
    mockRegister.mockRejectedValue(new Error(USERNAME_ALREADY_EXISTS));
    const onAuthenticated = renderAuthScreen();

    fireEvent.click(screen.getByTestId('tab-register'));
    fillCredentials();
    submit();

    await waitFor(() => {
      expect(screen.getByTestId('auth-error-banner')).toBeInTheDocument();
    });

    expect(errorBannerText()).toBe(DUPLICATE_USERNAME_MESSAGE);
    expect(onAuthenticated).not.toHaveBeenCalled();
  });

  it('never exposes the raw USERNAME_ALREADY_EXISTS token to the player', async () => {
    mockRegister.mockRejectedValue(new Error(USERNAME_ALREADY_EXISTS));
    renderAuthScreen();

    fireEvent.click(screen.getByTestId('tab-register'));
    fillCredentials();
    submit();

    await waitFor(() => {
      expect(screen.getByTestId('auth-error-banner')).toBeInTheDocument();
    });

    // Guards against the generic branch regressing back in — it would render
    // `Đăng nhập thất bại: USERNAME_ALREADY_EXISTS`.
    expect(errorBannerText()).not.toContain(USERNAME_ALREADY_EXISTS);
    expect(screen.queryByText(new RegExp(USERNAME_ALREADY_EXISTS))).toBeNull();
    expect(screen.queryByText(/USERNAME_ALREADY_TAKEN/)).toBeNull();
  });

  it('maps INVALID_CREDENTIALS from the login endpoint to its message', async () => {
    mockLogin.mockRejectedValue(new Error(INVALID_CREDENTIALS));
    renderAuthScreen();

    fillCredentials();
    submit();

    await waitFor(() => {
      expect(errorBannerText()).toBe(INVALID_CREDENTIALS_MESSAGE);
    });
  });

  it('maps INVALID_INPUT to its message', async () => {
    mockRegister.mockRejectedValue(new Error(INVALID_INPUT));
    renderAuthScreen();

    fireEvent.click(screen.getByTestId('tab-register'));
    fillCredentials();
    submit();

    await waitFor(() => {
      expect(errorBannerText()).toBe(INVALID_INPUT_MESSAGE);
    });
  });

  it('falls through to the generic branch for an unrecognized code', async () => {
    mockRegister.mockRejectedValue(new Error(UNRECOGNIZED_CODE));
    renderAuthScreen();

    fireEvent.click(screen.getByTestId('tab-register'));
    fillCredentials();
    submit();

    await waitFor(() => {
      expect(errorBannerText()).toBe(`Đăng nhập thất bại: ${UNRECOGNIZED_CODE}`);
    });
  });

  it('keeps the local validation gates ahead of any API call', async () => {
    renderAuthScreen();

    fireEvent.change(screen.getByTestId('input-username'), { target: { value: 'ab' } });
    fireEvent.change(screen.getByTestId('input-password'), { target: { value: 'secret123' } });
    submit();

    expect(errorBannerText()).toBe('Tên đăng nhập phải từ 3 đến 32 ký tự.');
    expect(mockLogin).not.toHaveBeenCalled();
    expect(mockRegister).not.toHaveBeenCalled();

    fireEvent.change(screen.getByTestId('input-username'), { target: { value: 'playerone' } });
    fireEvent.change(screen.getByTestId('input-password'), { target: { value: 'short' } });
    submit();

    expect(errorBannerText()).toBe('Mật khẩu phải có ít nhất 6 ký tự.');
    expect(mockLogin).not.toHaveBeenCalled();

    // The local gates reject before the request is ever issued, and the
    // successful path still reaches the API once both gates pass.
    mockLogin.mockResolvedValue({ token: 'session-token' });
    fireEvent.change(screen.getByTestId('input-password'), { target: { value: 'secret123' } });
    submit();

    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledTimes(1);
    });
  });
});
