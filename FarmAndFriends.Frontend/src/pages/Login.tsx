import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { loginRequest } from '../api/auth'
import { useAuth } from '../auth/useAuth'
import { AuthShell } from '../components/AuthShell'
import readyTomato from '../game/assets/tiles/ready/tomato.png'

type RegistrationNavigationState = {
  registered: true
  username: string
}

type LoginField = 'username' | 'password'
type LoginFieldErrors = Partial<Record<LoginField, string>>

type FocusRequest = {
  field: LoginField
  sequence: number
}

function readRegistrationState(state: unknown): RegistrationNavigationState | null {
  if (!state || typeof state !== 'object') return null

  const candidate = state as Record<string, unknown>
  if (candidate.registered !== true || typeof candidate.username !== 'string') {
    return null
  }

  const username = candidate.username.trim()
  return username ? { registered: true, username } : null
}

function getErrorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Não foi possível entrar. Tente novamente.'
}

export default function Login() {
  const location = useLocation()
  const registrationStateRef = useRef(readRegistrationState(location.state))
  const registrationState = registrationStateRef.current
  const [username, setUsername] = useState(
    () => registrationState?.username ?? '',
  )
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<LoginFieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [focusRequest, setFocusRequest] = useState<FocusRequest | null>(null)
  const [showRegistrationSuccess, setShowRegistrationSuccess] = useState(
    () => Boolean(registrationState),
  )
  const usernameRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)
  const focusSequenceRef = useRef(0)
  const submitInProgressRef = useRef(false)
  const { login } = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    const previousTitle = document.title
    document.title = 'Entrar | Farm & Friends'
    return () => {
      document.title = previousTitle
    }
  }, [])

  useEffect(() => {
    if (!registrationState) return

    navigate('/login', { replace: true, state: null })
  }, [navigate, registrationState])

  useEffect(() => {
    if (!focusRequest) return

    const refs = {
      username: usernameRef,
      password: passwordRef,
    }
    refs[focusRequest.field].current?.focus()
  }, [focusRequest])

  function clearFieldError(field: LoginField) {
    setFieldErrors(current => {
      if (!current[field]) return current

      const next = { ...current }
      delete next[field]
      return next
    })
    setFormError(null)
  }

  function focusFirstInvalidField(errors: LoginFieldErrors) {
    const field: LoginField | undefined = errors.username
      ? 'username'
      : errors.password
        ? 'password'
        : undefined

    if (!field) return

    focusSequenceRef.current += 1
    setFocusRequest({ field, sequence: focusSequenceRef.current })
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitInProgressRef.current) return

    setShowRegistrationSuccess(false)
    setFormError(null)

    const trimmedUsername = username.trim()
    const nextFieldErrors: LoginFieldErrors = {}
    if (!trimmedUsername) {
      nextFieldErrors.username = 'Informe seu nome de jogador.'
    }
    if (!password) {
      nextFieldErrors.password = 'Informe sua senha.'
    }

    setFieldErrors(nextFieldErrors)
    if (Object.keys(nextFieldErrors).length > 0) {
      focusFirstInvalidField(nextFieldErrors)
      return
    }

    submitInProgressRef.current = true
    setLoading(true)

    try {
      const response = await loginRequest({
        username: trimmedUsername,
        password,
      })
      localStorage.setItem('access_token', response.access_token)
      login(response.access_token)
      navigate('/game', { replace: true })
    } catch (loginError: unknown) {
      setFormError(getErrorMessage(loginError))
    } finally {
      submitInProgressRef.current = false
      setLoading(false)
    }
  }

  return (
    <AuthShell illustrationSrc={readyTomato}>
      <div className="auth-panel">
        <p className="auth-kicker">A porteira está aberta</p>
        <h1 className="auth-title">Bem vindo de volta</h1>
        <p className="auth-intro">
          Entre para cuidar das plantações, colher recompensas e visitar seus
          amigos.
        </p>

        {showRegistrationSuccess && (
          <p
            id="login-registration-status"
            className="auth-registration-status"
            role="status"
            aria-live="polite"
          >
            <span aria-hidden="true">🌻</span>
            <span>Conta criada! Entre para conhecer sua nova fazenda.</span>
          </p>
        )}

        <form
          className="auth-form"
          onSubmit={handleSubmit}
          noValidate
          aria-busy={loading}
        >
          <div className="auth-field">
            <label className="auth-label" htmlFor="login-username">
              Nome do jogador
            </label>
            <input
              ref={usernameRef}
              id="login-username"
              name="username"
              type="text"
              value={username}
              onChange={event => {
                setUsername(event.target.value)
                clearFieldError('username')
              }}
              placeholder="Seu nome no jogo"
              autoComplete="username"
              autoCapitalize="none"
              spellCheck={false}
              maxLength={30}
              required
              disabled={loading}
              aria-invalid={Boolean(fieldErrors.username)}
              aria-describedby={
                fieldErrors.username ? 'login-username-error' : undefined
              }
              className="auth-input"
            />
            {fieldErrors.username && (
              <p id="login-username-error" className="auth-field-error">
                {fieldErrors.username}
              </p>
            )}
          </div>

          <div className="auth-field">
            <label className="auth-label" htmlFor="login-password">
              Senha
            </label>
            <input
              ref={passwordRef}
              id="login-password"
              name="password"
              type="password"
              value={password}
              onChange={event => {
                setPassword(event.target.value)
                clearFieldError('password')
              }}
              placeholder="Sua senha"
              autoComplete="current-password"
              required
              disabled={loading}
              aria-invalid={Boolean(fieldErrors.password)}
              aria-describedby={
                fieldErrors.password ? 'login-password-error' : undefined
              }
              className="auth-input"
            />
            {fieldErrors.password && (
              <p id="login-password-error" className="auth-field-error">
                {fieldErrors.password}
              </p>
            )}
          </div>

          {formError && (
            <p className="auth-alert" role="alert">
              {formError}
            </p>
          )}

          <button type="submit" disabled={loading} className="auth-button">
            {loading && <span className="auth-button-spinner" aria-hidden="true" />}
            {loading ? 'Abrindo o portão…' : 'Entrar na fazenda'}
          </button>
        </form>

        <p className="auth-footer">
          Novo por aqui?{' '}
          <Link
            className="auth-link"
            to="/register"
            aria-disabled={loading}
            tabIndex={loading ? -1 : undefined}
            onClick={event => {
              if (loading) event.preventDefault()
            }}
          >
            Criar minha fazenda
          </Link>
        </p>
      </div>
    </AuthShell>
  )
}
