import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { registerRequest } from '../api/auth'
import { AuthShell } from '../components/AuthShell'
import plotGrowing from '../game/assets/tiles/plot-growing.png'
import apple_tree from '../game/assets/crops/mature/apple_tree.png'

type RegisterField =
  | 'username'
  | 'password'
  | 'confirmPassword'
  | 'farmname'

type FieldErrors = Partial<Record<RegisterField, string>>

type RegistrationSuccess = {
  username: string
  farmName: string
}

type FocusRequest = {
  field: RegisterField
  sequence: number
}

const SUCCESS_DELAY_MS = 3_000
const REGISTER_FIELD_ORDER: RegisterField[] = [
  'username',
  'password',
  'confirmPassword',
  'farmname',
]

function getErrorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Não foi possível criar sua conta. Tente novamente.'
}

export default function Register() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [farmname, setFarmName] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [success, setSuccess] = useState<RegistrationSuccess | null>(null)
  const [focusRequest, setFocusRequest] = useState<FocusRequest | null>(null)

  const usernameRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)
  const confirmPasswordRef = useRef<HTMLInputElement>(null)
  const farmnameRef = useRef<HTMLInputElement>(null)
  const successHeadingRef = useRef<HTMLHeadingElement>(null)
  const focusSequenceRef = useRef(0)
  const submitInProgressRef = useRef(false)
  const navigate = useNavigate()

  useEffect(() => {
    const previousTitle = document.title
    document.title = 'Criar fazenda | Farm & Friends'
    return () => {
      document.title = previousTitle
    }
  }, [])

  useEffect(() => {
    if (!success) return

    successHeadingRef.current?.focus()

    const timeoutId = window.setTimeout(() => {
      navigate('/login', {
        replace: true,
        state: {
          registered: true,
          username: success.username,
        },
      })
    }, SUCCESS_DELAY_MS)

    return () => window.clearTimeout(timeoutId)
  }, [navigate, success])

  useEffect(() => {
    if (!focusRequest) return

    const refs = {
      username: usernameRef,
      password: passwordRef,
      confirmPassword: confirmPasswordRef,
      farmname: farmnameRef,
    }
    refs[focusRequest.field].current?.focus()
  }, [focusRequest])

  function clearFieldError(field: RegisterField) {
    setFieldErrors(current => {
      if (!current[field]) return current

      const next = { ...current }
      delete next[field]
      return next
    })
    setFormError(null)
  }

  function validateForm(trimmedUsername: string, trimmedFarmName: string) {
    const errors: FieldErrors = {}

    if (!trimmedUsername) {
      errors.username = 'Escolha um nome para o jogador.'
    } else if (trimmedUsername.length > 30) {
      errors.username = 'Use no máximo 30 caracteres.'
    }

    if (!password.trim()) {
      errors.password = 'Crie uma senha.'
    }

    if (!confirmPassword) {
      errors.confirmPassword = 'Repita a senha.'
    } else if (password !== confirmPassword) {
      errors.confirmPassword = 'As senhas não coincidem.'
    }

    if (!trimmedFarmName) {
      errors.farmname = 'Dê um nome à sua fazenda.'
    } else if (trimmedFarmName.length > 30) {
      errors.farmname = 'Use no máximo 30 caracteres.'
    }

    return errors
  }

  function focusFirstInvalidField(errors: FieldErrors) {
    const firstInvalidField = REGISTER_FIELD_ORDER.find(field => errors[field])
    if (!firstInvalidField) return

    focusSequenceRef.current += 1
    setFocusRequest({
      field: firstInvalidField,
      sequence: focusSequenceRef.current,
    })
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitInProgressRef.current) return

    const trimmedUsername = username.trim()
    const trimmedFarmName = farmname.trim()
    const nextFieldErrors = validateForm(trimmedUsername, trimmedFarmName)

    setFieldErrors(nextFieldErrors)
    setFormError(null)

    if (Object.keys(nextFieldErrors).length > 0) {
      focusFirstInvalidField(nextFieldErrors)
      return
    }

    submitInProgressRef.current = true
    setLoading(true)

    try {
      const response = await registerRequest({
        username: trimmedUsername,
        password,
        farmname: trimmedFarmName,
      })

      setSuccess({
        username: response.username,
        farmName: trimmedFarmName,
      })
    } catch (error: unknown) {
      setFormError(getErrorMessage(error))
    } finally {
      submitInProgressRef.current = false
      setLoading(false)
    }
  }

  return (
    <AuthShell illustrationSrc={plotGrowing}>
      {success ? (
        <div
          className="auth-panel auth-success"
          role="status"
          aria-live="polite"
          aria-labelledby="registration-success-title"
        >
          <div className="auth-success-copy">
            <div className="auth-success-art" aria-hidden="true">
              <span className="auth-leaf auth-leaf-one">🍃</span>
              <span className="auth-leaf auth-leaf-two">🍃</span>
              <span className="auth-leaf auth-leaf-three">🍃</span>
              <img
                src={apple_tree}
                alt=""
                className="auth-success-illustration"
              />
            </div>

            <p className="auth-kicker">Terrinha preparada</p>
            <h1
              id="registration-success-title"
              ref={successHeadingRef}
              tabIndex={-1}
              className="auth-title"
            >
              Sua fazenda foi criada!
            </h1>
            <p className="auth-success-description">
              <strong>{success.username}</strong>, a fazenda{' '}
              <strong>{success.farmName}</strong> está pronta para receber você.
            </p>

            <div className="auth-redirect-status" aria-label="Abrindo a entrada">
              Abrindo a porteira
              <span className="auth-redirect-dot" aria-hidden="true" />
              <span className="auth-redirect-dot" aria-hidden="true" />
              <span className="auth-redirect-dot" aria-hidden="true" />
            </div>
          </div>
        </div>
      ) : (
        <div className="auth-panel">
          <p className="auth-kicker">Sua história começa aqui</p>
          <h1 className="auth-title">Comece sua fazenda</h1>
          <p className="auth-intro">
            Escolha seu nome, batize seu cantinho e prepare a terra para a
            primeira plantação.
          </p>

          <form
            className="auth-form"
            onSubmit={handleSubmit}
            noValidate
            aria-busy={loading}
          >
            <div className="auth-field">
              <label className="auth-label" htmlFor="register-username">
                Nome do jogador
              </label>
              <input
                ref={usernameRef}
                id="register-username"
                name="username"
                type="text"
                value={username}
                onChange={event => {
                  setUsername(event.target.value)
                  clearFieldError('username')
                }}
                placeholder="Ex.: João da Roça"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                required
                maxLength={30}
                disabled={loading}
                aria-invalid={Boolean(fieldErrors.username)}
                aria-describedby={
                  fieldErrors.username ? 'register-username-error' : undefined
                }
                className="auth-input"
              />
              {fieldErrors.username && (
                <p id="register-username-error" className="auth-field-error">
                  {fieldErrors.username}
                </p>
              )}
            </div>

            <div className="auth-field">
              <label className="auth-label" htmlFor="register-password">
                Senha
              </label>
              <input
                ref={passwordRef}
                id="register-password"
                name="password"
                type="password"
                value={password}
                onChange={event => {
                  setPassword(event.target.value)
                  clearFieldError('password')
                  clearFieldError('confirmPassword')
                }}
                placeholder="Crie uma senha"
                autoComplete="new-password"
                required
                disabled={loading}
                aria-invalid={Boolean(fieldErrors.password)}
                aria-describedby={
                  fieldErrors.password ? 'register-password-error' : undefined
                }
                className="auth-input"
              />
              {fieldErrors.password && (
                <p id="register-password-error" className="auth-field-error">
                  {fieldErrors.password}
                </p>
              )}
            </div>

            <div className="auth-field">
              <label className="auth-label" htmlFor="register-confirm-password">
                Confirmar senha
              </label>
              <input
                ref={confirmPasswordRef}
                id="register-confirm-password"
                name="confirmPassword"
                type="password"
                value={confirmPassword}
                onChange={event => {
                  setConfirmPassword(event.target.value)
                  clearFieldError('confirmPassword')
                }}
                placeholder="Digite a senha novamente"
                autoComplete="new-password"
                required
                disabled={loading}
                aria-invalid={Boolean(fieldErrors.confirmPassword)}
                aria-describedby={
                  fieldErrors.confirmPassword
                    ? 'register-confirm-password-error'
                    : undefined
                }
                className="auth-input"
              />
              {fieldErrors.confirmPassword && (
                <p
                  id="register-confirm-password-error"
                  className="auth-field-error"
                >
                  {fieldErrors.confirmPassword}
                </p>
              )}
            </div>

            <div className="auth-field">
              <label className="auth-label" htmlFor="register-farmname">
                Nome da fazenda
              </label>
              <input
                ref={farmnameRef}
                id="register-farmname"
                name="farmname"
                type="text"
                value={farmname}
                onChange={event => {
                  setFarmName(event.target.value)
                  clearFieldError('farmname')
                }}
                placeholder="Ex.: Cantinho do Sol"
                autoComplete="off"
                maxLength={30}
                required
                disabled={loading}
                aria-invalid={Boolean(fieldErrors.farmname)}
                aria-describedby={
                  fieldErrors.farmname ? 'register-farmname-error' : undefined
                }
                className="auth-input"
              />
              {fieldErrors.farmname && (
                <p id="register-farmname-error" className="auth-field-error">
                  {fieldErrors.farmname}
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
              {loading ? 'Preparando a terra…' : 'Criar minha fazenda'}
            </button>
          </form>

          <p className="auth-footer">
            Já tem uma fazenda?{' '}
            <Link
              className="auth-link"
              to="/login"
              aria-disabled={loading}
              tabIndex={loading ? -1 : undefined}
              onClick={event => {
                if (loading) event.preventDefault()
              }}
            >
              Entrar
            </Link>
          </p>
        </div>
      )}
    </AuthShell>
  )
}
