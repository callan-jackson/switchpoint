import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { errorMessage, isApiError } from '@/api/client'
import { useCreateClient, useUpdateClient } from '@/api/queries'
import type { ClientDetail, ClientWrite } from '@/api/types'
import {
  employmentStatusLabels,
  maritalStatusLabels,
  optionsFrom,
  sexLabels,
  taxRegimeLabels,
} from '@/lib/labels'
import { Alert } from '@/components/ui/Alert'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { useToast } from '@/components/ui/Toast'
import { Field } from '@/components/form/Field'
import { Checkbox, Input, Select } from '@/components/form/Input'
import { ControlledInteger, ControlledMoney } from '@/components/form/Controlled'

const schema = z.object({
  title: z.string().optional(),
  firstName: z.string().min(1, 'Enter a first name'),
  lastName: z.string().min(1, 'Enter a last name'),
  dateOfBirth: z.string().min(1, 'Enter a date of birth'),
  sex: z.enum(['male', 'female']),
  email: z.union([z.string().email('Enter a valid email address'), z.literal('')]).optional(),
  phone: z.string().optional(),
  maritalStatus: z.enum(['single', 'married', 'civilPartnership', 'divorced', 'widowed', 'cohabiting']),
  employmentStatus: z.enum(['employed', 'selfEmployed', 'retired', 'notWorking', 'director']),
  annualSalary: z.number().min(0, 'Cannot be negative').nullable(),
  targetRetirementAge: z
    .number()
    .int()
    .min(50, 'Must be 50 or later')
    .max(80, 'Must be 80 or earlier')
    .nullable()
    .refine((v) => v !== null, 'Enter a target retirement age'),
  taxRegime: z.enum(['restOfUk', 'scotland']),
  riskProfile: z
    .number()
    .int()
    .min(1, 'Risk profile runs from 1 to 7')
    .max(7, 'Risk profile runs from 1 to 7')
    .nullable()
    .refine((v) => v !== null, 'Enter a risk profile'),
  isSmoker: z.boolean(),
  statePensionForecastWeekly: z.number().min(0).nullable(),
  nationalInsuranceNumber: z.string().optional(),
})

/**
 * The numeric inputs store `number | null`, so the form's own value type is the schema's INPUT
 * type; `handleSubmit` hands back the parsed OUTPUT type. Spelling both out keeps react-hook-form
 * and the zod resolver in agreement.
 */
type ClientFormValues = z.input<typeof schema>
type ClientFormOutput = z.output<typeof schema>

function toValues(client?: ClientDetail): ClientFormValues {
  return {
    title: client?.title ?? '',
    firstName: client?.firstName ?? '',
    lastName: client?.lastName ?? '',
    dateOfBirth: client?.dateOfBirth ?? '',
    sex: client?.sex ?? 'female',
    email: client?.email ?? '',
    phone: client?.phone ?? '',
    maritalStatus: client?.maritalStatus ?? 'single',
    employmentStatus: client?.employmentStatus ?? 'employed',
    annualSalary: client?.annualSalary ?? 0,
    targetRetirementAge: client?.targetRetirementAge ?? 67,
    taxRegime: client?.taxRegime ?? 'restOfUk',
    riskProfile: client?.riskProfile ?? 4,
    isSmoker: client?.isSmoker ?? false,
    statePensionForecastWeekly: client?.statePension?.forecastWeeklyAmount ?? null,
    nationalInsuranceNumber: '',
  }
}

function toWrite(values: ClientFormOutput, existing?: ClientDetail): ClientWrite {
  return {
    title: values.title || undefined,
    firstName: values.firstName,
    lastName: values.lastName,
    dateOfBirth: values.dateOfBirth,
    sex: values.sex,
    email: values.email || undefined,
    phone: values.phone || undefined,
    address: existing?.address,
    maritalStatus: values.maritalStatus,
    employmentStatus: values.employmentStatus,
    annualSalary: values.annualSalary ?? 0,
    targetRetirementAge: values.targetRetirementAge ?? 67,
    taxRegime: values.taxRegime,
    riskProfile: values.riskProfile ?? 4,
    health: existing?.health ?? 'standard',
    isSmoker: values.isSmoker,
    statePension: {
      forecastWeeklyAmount: values.statePensionForecastWeekly ?? undefined,
      qualifyingYears: existing?.statePension?.qualifyingYears,
    },
    nationalInsuranceNumber: values.nationalInsuranceNumber || undefined,
  }
}

export interface ClientFormDialogProps {
  open: boolean
  onClose: () => void
  client?: ClientDetail
  onSaved?: (client: ClientDetail) => void
}

/** Add or edit the client record itself. Arrangements are managed on the Schemes tab. */
export function ClientFormDialog({ open, onClose, client, onSaved }: ClientFormDialogProps) {
  const toast = useToast()
  const create = useCreateClient()
  const update = useUpdateClient(client?.id ?? '')
  const saving = create.isPending || update.isPending

  const {
    register,
    control,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<ClientFormValues, unknown, ClientFormOutput>({
    resolver: zodResolver(schema),
    defaultValues: toValues(client),
  })

  useEffect(() => {
    if (open) reset(toValues(client))
  }, [open, client, reset])

  const onSubmit = handleSubmit(async (values) => {
    const body = toWrite(values, client)
    try {
      const saved = client ? await update.mutateAsync(body) : await create.mutateAsync(body)
      toast.success(client ? 'Client updated' : 'Client added', saved.fullName)
      onSaved?.(saved)
      onClose()
    } catch (error) {
      if (isApiError(error) && error.isValidation) {
        for (const [field, messages] of Object.entries(error.errors ?? {})) {
          if (field in schema.shape) setError(field as keyof ClientFormValues, { message: messages[0] })
        }
      }
      toast.error('Could not save the client', errorMessage(error))
    }
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      size="lg"
      locked={saving}
      title={client ? `Edit ${client.fullName}` : 'Add a client'}
      description="Details the calculation engines need: age, salary, tax regime and target retirement age."
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={onSubmit} loading={saving}>
            {client ? 'Save changes' : 'Add client'}
          </Button>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {(create.error || update.error) && (
          <Alert tone="danger" title="Could not save">
            {errorMessage(create.error ?? update.error)}
          </Alert>
        )}

        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Title" error={errors.title?.message}>
            <Input {...register('title')} placeholder="Ms" />
          </Field>
          <Field label="First name" required error={errors.firstName?.message}>
            <Input {...register('firstName')} />
          </Field>
          <Field label="Last name" required error={errors.lastName?.message}>
            <Input {...register('lastName')} />
          </Field>
          <Field label="Date of birth" required error={errors.dateOfBirth?.message}>
            <Input type="date" {...register('dateOfBirth')} />
          </Field>
          <Field label="Sex" required error={errors.sex?.message}>
            <Select {...register('sex')} options={optionsFrom(sexLabels)} />
          </Field>
          <Field label="National Insurance number" hint="Stored masked." error={errors.nationalInsuranceNumber?.message}>
            <Input {...register('nationalInsuranceNumber')} placeholder="QQ123456A" />
          </Field>
          <Field label="Email" error={errors.email?.message}>
            <Input type="email" {...register('email')} />
          </Field>
          <Field label="Phone" error={errors.phone?.message}>
            <Input type="tel" {...register('phone')} />
          </Field>
          <Field label="Marital status" error={errors.maritalStatus?.message}>
            <Select {...register('maritalStatus')} options={optionsFrom(maritalStatusLabels)} />
          </Field>
          <Field label="Employment status" error={errors.employmentStatus?.message}>
            <Select {...register('employmentStatus')} options={optionsFrom(employmentStatusLabels)} />
          </Field>
          <ControlledMoney control={control} name="annualSalary" label="Annual salary" dp={0} />
          <ControlledInteger control={control} name="targetRetirementAge" label="Target retirement age" required />
          <Field label="Tax regime" error={errors.taxRegime?.message}>
            <Select {...register('taxRegime')} options={optionsFrom(taxRegimeLabels)} />
          </Field>
          <ControlledInteger control={control} name="riskProfile" label="Risk profile (1–7)" required />
          <ControlledMoney
            control={control}
            name="statePensionForecastWeekly"
            label="State pension forecast"
            hint="Weekly amount"
          />
        </div>

        <Checkbox label="Smoker" {...register('isSmoker')} />
      </form>
    </Dialog>
  )
}
