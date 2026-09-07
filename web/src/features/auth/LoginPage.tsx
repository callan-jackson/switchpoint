import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, useLocation, useNavigate } from 'react-router'
import { ShieldCheck } from 'lucide-react'
import { errorMessage, isApiError } from '@/api/client'
import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { Field } from '@/components/form/Field'
import { Input } from '@/components/form/Input'
import { useAuth } from './AuthProvider'

const schema = z.object({
  email: z.string().min(1, 'Enter your email address').email('Enter a valid email address'),
  password: z.string().min(1, 'Enter your password'),
})

type LoginValues = z.infer<typeof schema>

const DEMO_EMAIL = 'adviser@demo.switchpoint.local'
const DEMO_PASSWORD = 'Demo!Pass123'

export default function LoginPage() {
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [formError, setFormError] = useState<string | null>(null)

  const from = (location.state as { from?: string } | null)?.from ?? '/'

  const {
    register,
    handleSubmit,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
    mode: 'onSubmit',
  })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      await signIn(values)
      navigate(from, { replace: true })
    } catch (error) {
      if (isApiError(error) && error.isValidation) {
        for (const [field, messages] of Object.entries(error.errors ?? {})) {
          if (field === 'email' || field === 'password') setError(field, { message: messages[0] })
        }
      }
      setFormError(
        isApiError(error) && error.status === 401
          ? 'Those details were not recognised. Check the email address and password and try again.'
          : errorMessage(error, 'Sign-in failed. Please try again.'),
      )
    }
  })

  return (
    <main className="flex min-h-screen items-center justify-center bg-surface-sunken px-4 py-10">
      <div className="w-full max-w-md">
        <div className="mb-6 flex items-center gap-3">
          <span className="flex size-10 items-center justify-center rounded-lg bg-primary-700 text-white">
            <ShieldCheck className="size-5" aria-hidden="true" />
          </span>
          <div>
            <p className="text-lg font-semibold tracking-tight text-fg">SwitchPoint</p>
            <p className="text-xs text-fg-muted">Pension switching and DB transfer analysis</p>
          </div>
        </div>

        <div className="card p-6">
          <h1 className="text-xl font-semibold tracking-tight text-fg">Sign in</h1>
          <p className="mt-1 text-sm text-fg-muted">Use your firm account to continue.</p>

          <form onSubmit={onSubmit} noValidate className="mt-5 space-y-4">
            {formError && (
              <Alert tone="danger" title="Could not sign in">
                {formError}
              </Alert>
            )}

            <Field label="Email address" required error={errors.email?.message}>
              <Input
                type="email"
                autoComplete="username"
                autoFocus
                placeholder="you@firm.co.uk"
                {...register('email')}
              />
            </Field>

            <Field label="Password" required error={errors.password?.message}>
              <Input type="password" autoComplete="current-password" {...register('password')} />
            </Field>

            <Button type="submit" size="lg" className="w-full" loading={isSubmitting}>
              Sign in
            </Button>
          </form>

          <div className="mt-5 rounded-lg border border-dashed border-border-strong bg-surface-muted p-3 text-xs text-fg-muted">
            <p className="font-semibold text-fg">Demo credentials</p>
            <p className="mt-1">
              <code className="font-mono">{DEMO_EMAIL}</code> — also <code className="font-mono">paraplanner@</code>{' '}
              and <code className="font-mono">compliance@</code>
              <br />
              Password <code className="font-mono">{DEMO_PASSWORD}</code>
            </p>
            <Button
              size="sm"
              variant="outline"
              className="mt-2"
              onClick={() => {
                setValue('email', DEMO_EMAIL)
                setValue('password', DEMO_PASSWORD)
              }}
            >
              Fill demo details
            </Button>
          </div>
        </div>

        <p className="mt-4 text-center text-xs text-fg-subtle">
          Analysis output is indicative and must be reviewed before it reaches a client.{' '}
          <Link to="/" className="underline">
            Back to the app
          </Link>
        </p>
      </div>
    </main>
  )
}
