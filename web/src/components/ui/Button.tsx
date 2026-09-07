import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Link, type LinkProps } from 'react-router'
import { Loader2 } from 'lucide-react'
import { cn } from '@/lib/cn'

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger' | 'accent'
export type ButtonSize = 'sm' | 'md' | 'lg'

const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-primary-700 text-white hover:bg-primary-600 active:bg-primary-800 shadow-sm',
  accent: 'bg-accent-500 text-white hover:bg-accent-600 active:bg-accent-700 shadow-sm',
  secondary: 'bg-primary-50 text-primary-700 hover:bg-primary-100 active:bg-primary-200',
  outline: 'border border-border-strong bg-surface text-fg hover:bg-surface-muted active:bg-surface-sunken',
  ghost: 'text-fg-muted hover:bg-surface-sunken hover:text-fg',
  danger: 'bg-danger-600 text-white hover:bg-danger-500 active:bg-danger-700 shadow-sm',
}

const sizeClasses: Record<ButtonSize, string> = {
  sm: 'h-8 px-2.5 text-xs gap-1.5',
  md: 'h-9 px-3.5 text-sm gap-2',
  lg: 'h-11 px-5 text-base gap-2',
}

export function buttonClasses(
  variant: ButtonVariant = 'primary',
  size: ButtonSize = 'md',
  extra?: string,
): string {
  return cn(
    'inline-flex items-center justify-center rounded-md font-medium whitespace-nowrap transition-colors',
    'disabled:pointer-events-none disabled:opacity-50',
    variantClasses[variant],
    sizeClasses[size],
    extra,
  )
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
  loading?: boolean
  icon?: ReactNode
  iconRight?: ReactNode
}

/** Standard action button. `loading` shows a spinner and disables the control. */
export function Button({
  variant = 'primary',
  size = 'md',
  loading = false,
  icon,
  iconRight,
  className,
  children,
  disabled,
  type = 'button',
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={buttonClasses(variant, size, className)}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...rest}
    >
      {loading ? (
        <Loader2 className="size-4 animate-spin" aria-hidden="true" />
      ) : (
        icon && <span className="[&>svg]:size-4" aria-hidden="true">{icon}</span>
      )}
      {children}
      {iconRight && <span className="[&>svg]:size-4" aria-hidden="true">{iconRight}</span>}
    </button>
  )
}

export interface ButtonLinkProps extends LinkProps {
  variant?: ButtonVariant
  size?: ButtonSize
  icon?: ReactNode
}

/** A router link styled as a button. */
export function ButtonLink({ variant = 'primary', size = 'md', icon, className, children, ...rest }: ButtonLinkProps) {
  return (
    <Link className={buttonClasses(variant, size, className)} {...rest}>
      {icon && <span className="[&>svg]:size-4" aria-hidden="true">{icon}</span>}
      {children}
    </Link>
  )
}

/** Icon-only button with an accessible name. */
export function IconButton({
  label,
  className,
  size = 'md',
  variant = 'ghost',
  children,
  ...rest
}: Omit<ButtonProps, 'icon' | 'children'> & { label: string; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      className={cn(
        'inline-flex items-center justify-center rounded-md transition-colors disabled:pointer-events-none disabled:opacity-50',
        variantClasses[variant],
        size === 'sm' ? 'size-8 [&>svg]:size-4' : 'size-9 [&>svg]:size-[18px]',
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  )
}
