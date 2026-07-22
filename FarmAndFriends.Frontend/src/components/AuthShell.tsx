import type { ReactNode } from 'react'

type AuthShellProps = {
  children: ReactNode
  illustrationSrc: string
}

export function AuthShell({ children, illustrationSrc }: AuthShellProps) {
  return (
    <main className="auth-page">
      <span className="auth-cloud auth-cloud-one" aria-hidden="true" />
      <span className="auth-cloud auth-cloud-two" aria-hidden="true" />

      <div className="auth-shell">
        <aside className="auth-hero" aria-label="Boas-vindas ao Farm & Friends">
          <div className="auth-brand">
            <span className="auth-brand-mark" aria-hidden="true">
              🌱
            </span>
            <div>
              <p className="auth-brand-name">Farm &amp; Friends</p>
              <p className="auth-brand-tagline">Sua fazenda, sua história</p>
            </div>
          </div>

          <div className="auth-illustration-stage" aria-hidden="true">
            <span className="auth-spark auth-spark-one">✦</span>
            <span className="auth-spark auth-spark-two">✦</span>
            <img
              key={illustrationSrc}
              src={illustrationSrc}
              alt=""
              className="auth-illustration"
            />
          </div>

          <div className="auth-hero-copy">
            <p className="auth-eyebrow">Um cantinho para chamar de seu</p>
            <p>
              Plante, colha e faça amizades em um mundo feito para crescer com
              você.
            </p>
          </div>
        </aside>

        <section className="auth-card">{children}</section>
      </div>
    </main>
  )
}
